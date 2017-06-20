# Setup

Pushes an image (test.jpg) to blob storage and then pushes a message onto the expense-capture function queue that starts the process of pushing images throught the ML pipeline.

To use this app, open the solution in Visual Studio (ensure you have the Azure SDK installed) and then open the code file 'program.cs'
In this code file, find the line that intialises the StorageAccount (line 21) and insert your connection string as per the storage account created as part of the Azure Function app expense-capture.

Press F5 to run the console app.

You can upload your own image by replacing the test.jpg included in the solution.
