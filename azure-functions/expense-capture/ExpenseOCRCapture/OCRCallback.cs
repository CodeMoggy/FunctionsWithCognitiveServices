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

using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System.Threading.Tasks;
using ExpenseOCRCapture.Model;

namespace ExpenseOCRCapture
{
    /// <summary>
    /// Provides the channel for the OCR Processor to communicate with the Expense Processor without unnecessary dependencies.
    /// </summary>
    public static class OCRCallback
    {
        [FunctionName("OCRCallback")]
        public async static Task<object> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "")] HttpRequestMessage req,
            [Table("receiptsTable", Connection = "AzureWebJobsStorage")] CloudTable receiptTable, 
            [Queue("receiptitems", Connection = "AzureWebJobsStorage")] ICollector<ReceiptQueueMessage> receiptQueue,
            TraceWriter log)
        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            OCRResult result = JsonConvert.DeserializeObject<OCRResult>(jsonContent);

            if (result.StatusCode == "Retry")
            {
                // there is a possibility the OCR processing failed because the service was unavailable or there was too many 
                // concurrent connections (this is something that can happen with Azure ML web services
                // therefore, rather than throwing an error, re-add the expense item back to the queue and try to re-process.

                // queue message to move to next step
                receiptQueue.Add(new ReceiptQueueMessage
                {
                    ExpenseId = result.ItemId,
                    Status = "Retry",
                    ProcessingStep = 99 // set to 99 to restart process
                });
            }
            else
            {
                // the process either succeeded or failed in which case update the table with the appropriate data or error text
                // inform the expense processor (via a queue message) that processing is complete

                // add row to results table
                var item = new ReceiptTableItem()
                {
                    PartitionKey = "key",
                    RowKey = result.ItemId,
                    ExpenseId = result.ItemId,
                    RawText = result.Text,
                    ErrorText = result.ErrorText,
                    ETag = "*"
                };

                var operation = TableOperation.Merge(item);
                await receiptTable.ExecuteAsync(operation);

                // queue message to move to next step
                receiptQueue.Add(new ReceiptQueueMessage
                {
                    ExpenseId = result.ItemId,
                    Status = result.StatusCode == "Success" ? "Complete" : "Error",
                    ProcessingStep = 1 // set to 1 to complete processing
                });
            }

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}



