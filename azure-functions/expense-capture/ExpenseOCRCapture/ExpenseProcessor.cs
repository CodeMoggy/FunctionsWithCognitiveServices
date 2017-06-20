//MIT License
//Copyright(c) 2017 Richard Custance
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using ExpenseOCRCapture.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Threading.Tasks;

namespace ExpenseOCRCapture
{
    /// <summary>
    /// Responsible for managing the flow all stages of the expense processing function.
    /// </summary>
    public static class ExpenseProcessor
    {
        [FunctionName("ExpenseProcessor")]
        public async static Task Run(
            [QueueTrigger("receiptitems", Connection = "AzureWebJobsStorage")]ReceiptQueueMessage receiptQueueItem,
            [Table("receiptsTable", Connection = "AzureWebJobsStorage")] CloudTable receiptsTable,
            [Queue("ocrqueue", Connection = "SmartServicesStorage")] ICollector<string> receiptOCRQueue,
            [Blob("incontainer/receipts", System.IO.FileAccess.Read, Connection = "AzureWebJobsStorage")] string receiptBlob,
            TraceWriter log)
        {
            log.Info($"Step is: {receiptQueueItem.ProcessingStep}");

            switch (receiptQueueItem.ProcessingStep)
            {
                case 0: // processing
                    log.Info($"Step 0 for item: {receiptQueueItem}");
                    await CreateTableEntry(receiptQueueItem, receiptsTable);
                    StartOCR(receiptQueueItem, receiptOCRQueue);
                    break;
                case 1: // complete
                    log.Info($"Processing Complete for item: {receiptQueueItem}");
                    UpdateTableStatus(receiptsTable, receiptQueueItem.Status, receiptQueueItem.ExpenseId);
                    break;
                case 99: // retrying
                    log.Info($"Retrying Processing for item: {receiptQueueItem}");
                    UpdateTableStatus(receiptsTable, "Retrying", receiptQueueItem.ExpenseId);
                    StartOCR(receiptQueueItem, receiptOCRQueue);
                    break;
                default:
                    break;
            }
        }

        private async static void UpdateTableStatus(CloudTable receiptsTable, string status, string id)
        {
            // update row int results table
            var item = new ReceiptTableItem()
            {
                PartitionKey = "key",
                RowKey = id,
                Status = status,
                ETag = "*"
            };

            var operation = TableOperation.Merge(item);
            await receiptsTable.ExecuteAsync(operation);
        }

        private static void StartOCR(ReceiptQueueMessage receiptQueueItem, ICollector<string> receiptOCRQueue)
        {
            // add message to OCR queue
            var OCRCallbackKey = ConfigurationManager.AppSettings["OCRCallbackKey"];
            var baseCallbackAddress = ConfigurationManager.AppSettings["BaseCallbackAddress"];
            string imageUrl = string.Empty;

            if (TryGetBlobLink(receiptQueueItem, out imageUrl))
            {
                var message = new OCRQueueMessage()
                {
                    ItemId = receiptQueueItem.ExpenseId,
                    ItemType = "receipt",
                    ImageUrl = imageUrl,
                    Callback = $"{baseCallbackAddress}/ocrCallback?code={OCRCallbackKey}"
                };

                receiptOCRQueue.Add(JsonConvert.SerializeObject(message));
            }
        }

        private static bool TryGetBlobLink(ReceiptQueueMessage receiptQueueItem, out string imageUrl)
        {
            var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference("receipts");
            imageUrl = string.Empty;

            if (blobContainer.Exists())
            {
                var blob = blobContainer.GetBlockBlobReference(receiptQueueItem.ExpenseId);

                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
                sasConstraints.SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5);
                sasConstraints.SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24);
                sasConstraints.Permissions = SharedAccessBlobPermissions.Read;

                string sasBlobToken = blob.GetSharedAccessSignature(sasConstraints);

                imageUrl = blob.Uri + sasBlobToken;
            }

            return !string.IsNullOrEmpty(imageUrl);
        }

        private static async Task CreateTableEntry(ReceiptQueueMessage receiptQueueItem, CloudTable receiptsTable)
        {
            // create table entry
            var item = new ReceiptTableItem()
            {
                PartitionKey = "key",
                RowKey = receiptQueueItem.ExpenseId,
                ExpenseId = receiptQueueItem.ExpenseId,
                UserId = receiptQueueItem.UserId,
                Status = "Processing"
            };

            var operation = TableOperation.Insert(item);
            await receiptsTable.ExecuteAsync(operation);
        }
    }
}
