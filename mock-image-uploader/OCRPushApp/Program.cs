using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Queue;


namespace MockCaptureConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // expense item uid + identifier for blob
            var expenseId = Guid.NewGuid().ToString();
            var userId = "Michael";

            var storageAccount = CloudStorageAccount.Parse("Enter the value of the ExpenseOCRCapture Function App AzureWebJobsStorage app setting - this is storage connection string");

            var blobClient = storageAccount.CreateCloudBlobClient();

            var blobContainer = blobClient.GetContainerReference("receipts");
            blobContainer.CreateIfNotExists();
            string imageName = expenseId + ".jpg";

            var block = blobContainer.GetBlockBlobReference(imageName);

            block.Properties.ContentType = "image/jpg";
            block.UploadFromFile("./test.jpg");

            var client = storageAccount.CreateCloudQueueClient();

            var queue = client.GetQueueReference("receiptitems");

            queue.CreateIfNotExists();

            var message = new ReceiptQueueMessage
            {
                ExpenseId = imageName,
                UserId = userId,
                ProcessingStep = 0
            };

            var queueMessage = new CloudQueueMessage(Newtonsoft.Json.JsonConvert.SerializeObject(message));

            queue.AddMessage(queueMessage);

        }
    }

    public class ReceiptQueueMessage
    {
        public string ExpenseId { get; set; }
        public string UserId { get; set; }
        public int ProcessingStep { get; set; }
    }
}
