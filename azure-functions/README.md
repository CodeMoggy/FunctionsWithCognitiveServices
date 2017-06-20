# Setup

The following Visual Studio solutions are the Azure Function projects used to process receipt images through the OCR pipeline as described in the ML folder as part of this repository.
As of 05/25/2017, to open the projects requires you to download Visual Studio 2017 Preview (2) and install the Azure Functions SDK. Steps to do so can be found here:

https://blogs.msdn.microsoft.com/webdev/2017/05/10/azure-function-tools-for-visual-studio-2017/

Once you have completed the setup you can open each solution and review the code.

The Expense-Capture solution is responsible for pushing the receipt image through the ML pipeline. The workflow relies on a message being pushed onto a queue (a sample console application can be found in the Image-Uploader folder in this repository). Each time a message is pushed onto a queue the ExpenseProcessor function is triggered and begins the process of notifying the Smart-Services functions that there is a image to process. It does so by pushing a message on to a queue that is being monitored by the Smart-Service function called PerforCustomML...this function is reponsible for calling the ML webservice and then returning the result back to a HttpTrigger function back in the Expense-Capture project.
For full details of the workflow please refer to the workflow diagram in this folder.

To build and publish the 2 function apps to Azure, right-click each project and choose publish. Follow the publish wizard to complete the process.

Once both function apps have been published there are several Application Settings in each Function App that need completing:

Smart-Services
 - MLAPIUrl (refers to the ML Web Service that will be called as part of the PerformCustomML)
 - MLAPIKey (refers to the ML Web Service key required to call the service...this is required by the function PerformCustomML)
 
Expense-Capture
 - SmartServicesStorage (refers to the value of the AzureWebJobsStorage used by the Smart-Services Function app)
 - MLCallbackKey (refers to the key value of the function CustomMLCallback...to get this value click the CustomMLCallback fucntion in the Azure Portal and choose the 'Manage' tab. Copy the 'default' Function Keys value)
 - BaseCallbackAddress (refers to the url where the functions are hosted + the segment /api, e.g. https://yourfunctionapp.azurewebsites.net/api)
 
As an additional step you may also want to consider enabling continuous-integration (auto-deploy) for each function app. To do so I would recommend following the following guidance found in this resource:

https://www.joshcarlisle.io/blog/2017/5/17/visual-studio-2017-tools-for-azure-functions-and-continuous-integration-with-vsts?utm_content=buffer49bb0&utm_medium=social&utm_source=twitter.com&utm_campaign=buffer
