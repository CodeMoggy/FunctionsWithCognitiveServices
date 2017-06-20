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

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using SmartOCRService.Model;
using SmartOCRService.Util;
using System;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SmartOCRService
{
    /// <summary>
    /// Responsible for processing the OCR request. Standalone services that is triggered by a message on the OCR queue item
    /// Uses a preconfigured callback URL to pass the processed data back to the calling application
    /// </summary>
    public static class PerformOCR
    {
        [FunctionName("PerformOCR")]
        public async static Task Run([QueueTrigger("ocrqueue", Connection = "AzureWebJobsStorage")]OCRQueueMessage myQueueItem,
            int dequeueCount,
            TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {myQueueItem}");

            OCRResult res = await MakeOCRRequest(myQueueItem.ItemId, myQueueItem.ImageUrl, dequeueCount, log);

            // perform callback
            using (var client = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(res), Encoding.UTF8, "application/json");

                log.Info($"callback: {myQueueItem.Callback}");
                log.Info($"image: {myQueueItem.ImageUrl}");

                var result = await client.PostAsync(myQueueItem.Callback, content);

                log.Info($"result: {result.StatusCode}");
            }
        }

        static async Task<OCRResult> MakeOCRRequest(string itemId, string imageFilePath, int dequeueCount, TraceWriter log)
        {
            OCRResult res = new OCRResult();

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", ConfigurationManager.AppSettings["CogServicesKey"]);

                    string uri = ConfigurationManager.AppSettings["CogServicesAPI"];

                    HttpResponseMessage response;

                    // convert image into a byte array to pass as a parameter to the OCR API
                    byte[] byteData = await GetImageAsByteArray(imageFilePath);

                    using (var content = new ByteArrayContent(byteData))
                    {
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        response = await client.PostAsync(uri, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var contents = await response.Content.ReadAsStringAsync();

                            log.Info($"Raw receipt text is {contents}");

                            string text = ParseData(contents);

                            log.Info($"Parsed receipt text is {text}");

                            res.Text = text;
                            res.StatusCode = "Success";
                            res.ItemId = itemId;
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                        {
                            // The ML webservice has constraints on the number of concurrent calls that can be made against the service. 
                            // If the max number of calls are made (currently 20) then the service returns a status of ServiceUnavailable.
                            // By default the function will retry 5 times if there is a failure executing, once that retry limit has been met 
                            // the queue message is automatically added to a poison queue. We want to intervene at this point. Rather than add it to the 
                            // poison queue we want to retry the process again, but we will let the capture services govern how that retry is done
                            if (dequeueCount < 4)
                            {
                                throw new RetryException();
                            }
                            else
                            {
                                throw new MaxRetryException();
                            }
                        }
                        else
                        {
                            log.Info(string.Format("The request failed with status code: {0}", response.StatusCode));
                            string responseContent = await response.Content.ReadAsStringAsync();
                            log.Info($"content: {responseContent}");
                            throw new Exception("Error calling OCR API : " + responseContent);
                        }
                    }
                }
            }
            catch (RetryException)
            {
                // by throwing an exception, the queue message is retried 
                throw;
            }
            catch (MaxRetryException)
            {
                // maximum number of retries achieved, let the capture service handle what to do next
                res.StatusCode = "Retry";
                res.ItemId = itemId;
            }
            catch (Exception ex)
            {
                // a catastophic error has occurred, let the capture app fail gracefully
                res.StatusCode = "Error";
                res.ErrorText = ex.Message;
                res.ItemId = itemId;
            }

            return res;
        }

        /// <summary>
        /// Responsible for removing white space and empty lines
        /// </summary>
        /// <param name="contents"></param>
        /// <returns>text</returns>
        private static string ParseData(string contents)
        {
            dynamic json = JsonConvert.DeserializeObject<dynamic>(contents);

            string text = "";
            bool newline = true;

            foreach (var r in json.regions)
            {
                foreach (var l in r.lines)
                {
                    foreach (var w in l.words)
                    {
                        if (newline)
                        {
                            text += w.text;
                            newline = false;
                        }
                        else
                        {
                            text += " " + w.text;
                        }
                    }
                    text += System.Environment.NewLine;
                    newline = true;
                }
            }

            return text;
        }

        /// <summary>
        /// Convert the imgae to a byte array
        /// </summary>
        /// <param name="imageFilePath"></param>
        /// <returns>byte[]</returns>
        static async Task<byte[]> GetImageAsByteArray(string imageFilePath)
        {
            var httpClient = new HttpClient();
            return await httpClient.GetByteArrayAsync(new Uri(imageFilePath));
        }
    }
}
