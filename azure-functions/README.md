# Pre-requisites

The following code projects describe how to configure and deploy Azure Functions to enable processing of receipt images through an OCR pipeline similar to that described in the ML folder as part of this GitHub repository.

1. Note, to use the projects there is not a dependency to have to use the ML code provided. Instead I have configured the OCRSmartService to callout to the Microsoft Cognitive Service OCR API as described here https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/quickstarts/csharp#OCR. You will need to make a note of your API key and the API Url. Please refer to this documentation to find out where you can find the API endpoints and your subscription key: https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/quickstarts/csharp

2. As of writing this code, to open the projects requires you to download Visual Studio 2017 Preview (2) and install the Azure Functions SDK. Steps to do so can be found here https://blogs.msdn.microsoft.com/webdev/2017/05/10/azure-function-tools-for-visual-studio-2017/. 
Once you have completed the setup you can open each solution and review the code.

# Setup

The Expense-Capture solution is responsible for pushing the receipt image through the OCR pipeline. The workflow relies on a message being pushed onto a queue (a sample console application can be found in the Image-Uploader folder in this repository). Each time a message is pushed onto a queue the ExpenseProcessor function is triggered and begins the process of notifying the Smart-Services functions that there is a image to process. It does so by pushing a message on to a queue that is being monitored by the Smart-Service function called PerformOCR...this function is reponsible for calling the OCR webservice and then returning the result back to a HttpTrigger function using a callback URL hosting the OCRCallback Azure Function.

To build and publish the 2 function apps to Azure, right-click each project and choose publish. Follow the publish wizard to complete the process.

Once both function apps have been published visit https://portal.azure.com and locate the new services. There are several Application Settings in each Function App that need completing:

SmartOCRService
 - CogServicesAPI (refers to the Url endpoint the Micosoft Cognitive Services Vision API is hosted)
 - CogServicesKey (refers to the Microsoft Cognitives Service key issued when you signed up as developer - as above)
 
ExpenseOCRCapture
 - SmartServicesStorage (refers to the value of the AzureWebJobsStorage connection string used by the SmartOCRService Function app)
 - OCRCallbackKey (refers to the key value of the function OCRCallback...to get this value click the OCRCallback function in the Azure Portal and choose the 'Manage' tab. Copy the 'default' Function Keys value)
 - BaseCallbackAddress (refers to the url where the functions are hosted + the segment /api, e.g. https://yourfunctionapp.azurewebsites.net/api)
 
## Extras

As an additional step you may also want to consider enabling continuous-integration (auto-deploy) for each function app. To do so I would recommend following the following guidance found in this resource:

https://www.joshcarlisle.io/blog/2017/5/17/visual-studio-2017-tools-for-azure-functions-and-continuous-integration-with-vsts?utm_content=buffer49bb0&utm_medium=social&utm_source=twitter.com&utm_campaign=buffer
