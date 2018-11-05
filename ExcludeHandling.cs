/***********************************************************************************************************************************
*                                                 GOD First                                                                        *
* Author: Dustin Ledbetter                                                                                                         *
* Release Date: 10-30-2018                                                                                                         *
* Version: 1.0                                                                                                                     *
* Purpose: This extension is used to exclude handling on certain items from the store when there metadata says to,                 *
* BUT only if they are sold without any other items                                                                                *
************************************************************************************************************************************/

/*
 References: There are three dlls referenced by this template:
    1. PageflexServices.dll
    2. StorefrontExtension.dll
    3. SXI.dll
 */

using Pageflex.Interfaces.Storefront;
using PageflexServices;
using System;
using System.IO;


namespace ExcludeHandlingExtension
{
    public class ExcludeHandling : SXIExtension
    {

        #region |--Fields--|
        // This section holds variables for code used throughout the program for quick refactoring as needed

        // These fields need to be created for the new storefront
        private const string _UNIQUENAME = @"Exclude.Handling.Extension";
        private const string _DISPLAYNAME = @"Services: Exclude Handling Extension";

        // These fields are used to set the location of the log files up based on the current storefront
        private static readonly string LOG_FILENAME1 = "D:\\Pageflex\\Deployments\\";
        private static readonly string LOG_FILENAME2 = "\\Logs\\Exclude_Handling_Extension_Log_File_";

        // This variable does not need to be changed
        private const string _EH_DEBUGGING_MODE = @"EHDebuggingMode";

        // This is a flag to keep track of if we should have a handling fee for the order or not
        private static string extensionYesFlag = "No";
        private static string extensionNoFlag = "No";
        private static double storeHandlingCharge;

        // These are used for our try catch block return values
        public static string ErrorUserID;
        public static string ErrorOrderID;
        public static string ErrorDocID;
        public static string ErrorProductID;
        public static string ErrorProductName;
        public static string ErrorProductSKU;
        public static string ErrorStoreExcludeHandlingFlag;

        #endregion


        #region |--Properties--|
        // At a minimum your extension must override the DisplayName and UniqueName properties.


        // The UniqueName is used to associate a module with any data that it provides to Storefront.
        public override string UniqueName
        {
            get
            {
                return _UNIQUENAME;
            }
        }

        // The DisplayName will be shown on the Extensions and Site Options pages of the Administrator site as the name of your module.
        public override string DisplayName
        {
            get
            {
                return _DISPLAYNAME;
            }
        }

        // Gets the parameter to determine if in debug mode or not. Can also be used to get more variables at one as well
        protected override string[] PARAMS_WE_WANT
        {
            get
            {
                return new string[1]
                {
                  _EH_DEBUGGING_MODE
                };
            }
        }

        // Used to access the storefront to retrieve variables
        ISINI SF { get { return Storefront; } }

        // This Method is used to write all of our logs to a txt file
        public void LogMessageToFile(string msg)
        {
            // Get the Date and time stamps as desired
            string currentLogDate = DateTime.Now.ToString("MMddyyyy");
            string currentLogTimeInsertMain = DateTime.Now.ToString("HH:mm:ss tt");

            // Get the storefront's name from storefront to send logs to correct folder
            string sfName = SF.GetValue(FieldType.SystemProperty, SystemProperty.STOREFRONT_NAME, null);

            // Setup Message to display in .txt file 
            msg = string.Format("Time: {0:G}:  Message: {1}{2}", currentLogTimeInsertMain, msg, Environment.NewLine);

            // Add message to the file 
            File.AppendAllText(LOG_FILENAME1 + sfName + LOG_FILENAME2 + currentLogDate + ".txt", msg);
        }

        #endregion


        #region |--This section setups up the extension config page on the storefront to takes input for variables from the user at setup to be used in our extension--|

        // This section sets up on the extension page on the storefront a check box for users to turn on or off debug mode and text fields to get logon info for DB and Avalara
        public override int GetConfigurationHtml(KeyValuePair[] parameters, out string HTML_configString)
        {
            // Load and check if we already have a parameter set
            LoadModuleDataFromParams(parameters);

            // If not then we setup one 
            if (parameters == null)
            {
                SConfigHTMLBuilder sconfigHtmlBuilder = new SConfigHTMLBuilder();
                sconfigHtmlBuilder.AddHeader();

                // Add checkbox to let user turn on and off debug mode
                sconfigHtmlBuilder.AddServicesHeader("Debug Mode:", "");
                sconfigHtmlBuilder.AddCheckboxField("Debugging Information", _EH_DEBUGGING_MODE, "true", "false", (string)ModuleData[_EH_DEBUGGING_MODE] == "true");
                sconfigHtmlBuilder.AddTip(@"This box should be checked if you wish for debugging information to be output to the Logs.");

                // Footer info and set to configstring
                sconfigHtmlBuilder.AddServicesFooter();
                HTML_configString = sconfigHtmlBuilder.html;
            }
            // If we do then move along
            else
            {
                SaveModuleData();
                HTML_configString = null;
            }
            return 0;
        }

        #endregion


        // Step 1 and 2 are reversed because step 2 comes second, but is called in step 1 -- methods should be placed before they are called
        #region |--Step 2: Validate Document Section (Called to check what the items in the cart have their exclude handlig flags set to)--|

        // Checks each document (item in the shipping cart) to see if it has the flag to exclude handling set or not when called
        public int ValidateDocument(string docID, string action)
        {
            // Place in try catch to ensure we are alerted if something goes wrong
            try
            {

                // Set our error values for catch blocks
                ErrorDocID = docID;
                ErrorProductID = Storefront.GetValue(FieldType.DocumentProperty, DocumentProperty.PRODUCT_ID, ErrorDocID);
                ErrorProductName = Storefront.GetValue(FieldType.ProductProperty, ProductProperty.DISPLAY_NAME, ErrorProductID);
                ErrorProductSKU = Storefront.GetValue(FieldType.ProductField, "PRODUCT_SKU", ErrorProductID);
                ErrorStoreExcludeHandlingFlag = Storefront.GetValue(FieldType.ProductField, "excludeHandling", ErrorProductID);

                // Get the values from the storefront to use to see if our order needs to have the handling excluded or not
                var productID = Storefront.GetValue(FieldType.DocumentProperty, DocumentProperty.PRODUCT_ID, docID);
                var productName = Storefront.GetValue(FieldType.ProductProperty, ProductProperty.DISPLAY_NAME, productID);
                var productSKU = Storefront.GetValue(FieldType.ProductField, "PRODUCT_SKU", productID);
                var storeExcludeHandlingFlag = Storefront.GetValue(FieldType.ProductField, "excludeHandling", productID);

                // Log messages to the store and to the .txt file to show what we have retrieved 
                if ((string)ModuleData[_EH_DEBUGGING_MODE] == "true")
                {
                    // Log messages to the storefront "Logs" page
                    LogMessage($".....................................................");
                    LogMessage($"-----------------------------------------------------");
                    LogMessage($"Step 2: Validating Document:     {docID}");
                    LogMessage($"Product ID:                      {productID}");
                    LogMessage($"Product Name:                    {productName}");
                    LogMessage($"Product SKU:                     {productSKU}");
                    LogMessage($"Exclude Handling Flag:           {storeExcludeHandlingFlag}");

                    // Log messages to the .txt file
                    LogMessageToFile($".....................................................");
                    LogMessageToFile($"-----------------------------------------------------");
                    LogMessageToFile($"Step 2: Validating Document:     {docID}");
                    LogMessageToFile($"Product ID:                      {productID}");
                    LogMessageToFile($"Product Name:                    {productName}");
                    LogMessageToFile($"Product SKU:                     {productSKU}");
                    LogMessageToFile($"Exclude Handling Flag:           {storeExcludeHandlingFlag}");

                }

                // Check to see if the storefront retrieved exclude handling fee flag is set to yes or no
                if (storeExcludeHandlingFlag.Equals("Yes"))
                {
                    // If it is set to exclude the handling then we set our extension YES flag to be "Yes"
                    extensionYesFlag = "Yes";
                }
                else if (storeExcludeHandlingFlag.Equals("No"))
                {
                    // If it is set to NOT exclude the handling then we set our extension NO flag to be "Yes"
                    extensionNoFlag = "Yes";
                }

                // Log messages to the store and to the .txt file to show what the flags have been set to
                if ((string)ModuleData[_EH_DEBUGGING_MODE] == "true")
                {
                    // Log messages to the storefront "Logs" page
                    LogMessage($"Validate Extension Yes Flag Set: {extensionYesFlag}");
                    LogMessage($"Validate Extension No Flag Set:  {extensionNoFlag}");

                    // Log messages to the .txt file
                    LogMessageToFile($"Validate Extension Yes Flag Set: {extensionYesFlag}");
                    LogMessageToFile($"Validate Extension No Flag Set:  {extensionNoFlag}");
                }

                // Send the response that this section is complete whether works or not so it doesn't crash
                return eSuccess;

            }
            catch
            {
                // Log issue with storefront and to file regardless of whether in debug mode or not
                LogMessage("Error in ValidateDocument Method");                                        // This logs that there was an error in the ValidateDocument Method
                LogMessageToFile("Error in ValidateDocument Method");                                  // This logs that there was an error in the ValidateDocument Method

                // Get the storefront's name from storefront and Date and time stamps as desired
                string sfName = SF.GetValue(FieldType.SystemProperty, SystemProperty.STOREFRONT_NAME, null);
                string currentLogDate = DateTime.Now.ToString("MMddyyyy");
                string currentLogTimeInsertMain = DateTime.Now.ToString("HH:mm:ss tt");

                //Setup our date and time for error
                string ErrorDate = string.Format("Date: {0}  Time: {1:G} <br>", currentLogDate, currentLogTimeInsertMain);
    
                // Setup our email body and message
                string subjectstring = "Storefront: \"" + sfName + "\" had an ERROR occur in the ValidateDocument Method";
                string bodystring = "Storefront: \"" + sfName + "\" had an ERROR occur in the ValidateDocument Method <br>" +
                                    ErrorDate +
                                    "Extension: Exclude Handling Extension <br>" +
                                    "ERROR occured with User ID: " + ErrorUserID + "<br>" +
                                    "ERROR occured with Order ID: " + ErrorOrderID + "<br>" +
                                    "ERROR occured with Doc ID: " + ErrorDocID + "<br>" +
                                    "ERROR occured with Error Product ID: " + ErrorProductID + "<br>" +
                                    "ERROR occured with Error Product Name : " + ErrorProductName + "<br>" +
                                    "ERROR occured with Error Product SKU: " + ErrorProductSKU + "<br>" +
                                    "ERROR occured with Error Store Exclude Handling Flag: " + ErrorStoreExcludeHandlingFlag;

                // Call method to send our error as an email to developers maintaining sites
                EmailErrorNotify.CreateMessage(subjectstring, bodystring);

                // Log issue with storefront and to file regardless of whether in debug mode or not
                LogMessage($"Error in ValidateDocument Method send email method called");              // This logs that Error in ValidateDocument Method send email method called
                LogMessageToFile($"Error in ValidateDocument Method send email method called");        // This logs that Error in ValidateDocument Method send email method called

                LogMessage($"Email sent successfully: {EmailErrorNotify.checkFlag}");                  // This logs Email sent successfully flag response 
                LogMessageToFile($"Email sent successfully: {EmailErrorNotify.checkFlag}");            // This logs Email sent successfully flag response 

                // Send the response validation is complete
                return eSuccess;
            }
        }

        #endregion


        #region |--Step 1: Check out steps before Section is used to get the storefronts current shipping amount and to call the validateDocuments method--|

        // This is called when the user hits the "Proceed to Checkout" button on the storefront
        public override int CheckoutSteps_Before(string userID, string orderID, string[] docIds)
        {

                // Log messages to the store and to the .txt file to alert we are about to start this process
                if ((string)ModuleData[_EH_DEBUGGING_MODE] == "true")
                {
                    // Log messages to the storefront "Logs" page
                    LogMessage($"-----------------------------------------------------");
                    LogMessage($"Step 1: Try to get the storefront handling charge");

                    // Log messages to the .txt file
                    LogMessageToFile($"-----------------------------------------------------");
                    LogMessageToFile($"Step 1: Try to get the storefront handling charge");
                }

            // Place in try catch to ensure we are alerted if something goes wrong
            try
            {

                // Set our error values for catch blocks
                ErrorUserID = userID;
                ErrorOrderID = orderID;

                // Get the storefront handling charge to use to keep it the same if we don't need to override it
                storeHandlingCharge = Convert.ToDouble(Storefront.GetValue(FieldType.OrderProperty, "HandlingCharge", orderID));

                // Log messages to the store and to the .txt file to show the storefronts original handling charge
                if ((string)ModuleData[_EH_DEBUGGING_MODE] == "true")
                {
                    // Log messages to the storefront "Logs" page
                    LogMessage($"Storefront Handling Charge:      {storeHandlingCharge}");
                    LogMessageToFile($"Storefront Handling Charge:      {storeHandlingCharge}");

                    // Log messages to the .txt file
                    LogMessage($"Storefront Handling Charge:      {storeHandlingCharge}");
                    LogMessageToFile($"Storefront Handling Charge:      {storeHandlingCharge}");
                }

                // Set These values to start as "No"
                extensionYesFlag = "No";
                extensionNoFlag = "No";

                // We need to check each item in the cart
                foreach (var docID in docIds)
                {
                    // Call to check if this item has the flag set to yes or not
                    ValidateDocument(docID, "check");
                }

                // Log messages to the store and to the .txt file to show that the flags are still set here
                // I had to use these to figure out that my variables for the flags were not being passed between methods because they were not static
                if ((string)ModuleData[_EH_DEBUGGING_MODE] == "true")
                {
                    // Log messages to the storefront "Logs" page
                    LogMessage($"-----------------------------------------------------");
                    LogMessage($"Foreach Extension Yes Flag Set:  {extensionYesFlag}");
                    LogMessage($"Foreach Extension No Flag Set:   {extensionNoFlag}");

                    // Log messages to the .txt file
                    LogMessageToFile($"-----------------------------------------------------");
                    LogMessageToFile($"Foreach Extension Yes Flag Set:  {extensionYesFlag}");
                    LogMessageToFile($"Foreach Extension No Flag Set:   {extensionNoFlag}");
                }

                // Send the response that the steps before checkout have been completed
                return eSuccess;

            }
            catch
            {
                // Log issue with storefront and to file regardless of whether in debug mode or not
                LogMessage("Error in CheckoutSteps_Before Method");                                    // This logs that there was an error in the CheckoutSteps_Before Method
                LogMessageToFile("Error in CheckoutSteps_Before Method");                              // This logs that there was an error in the CheckoutSteps_Before Method

                // Get the storefront's name from storefront and Date and time stamps as desired
                string sfName = SF.GetValue(FieldType.SystemProperty, SystemProperty.STOREFRONT_NAME, null);
                string currentLogDate = DateTime.Now.ToString("MMddyyyy");
                string currentLogTimeInsertMain = DateTime.Now.ToString("HH:mm:ss tt");

                //Setup our date and time for error
                string ErrorDate = string.Format("Date: {0}  Time: {1:G} <br>", currentLogDate, currentLogTimeInsertMain);

                // Setup our email body and message
                string subjectstring = "Storefront: \"" + sfName + "\" had an ERROR occur in the CheckoutSteps_Before Method";
                string bodystring = "Storefront: \"" + sfName + "\" had an ERROR occur in the CheckoutSteps_Before Method <br>" +
                                    ErrorDate +
                                    "Extension: Exclude Handling Extension <br>" +
                                    "ERROR occured with User ID: " + ErrorUserID + "<br>" +
                                    "ERROR occured with Order ID: " + ErrorOrderID;

                // Call method to send our error as an email to developers maintaining sites
                EmailErrorNotify.CreateMessage(subjectstring, bodystring);

                // Log issue with storefront and to file regardless of whether in debug mode or not
                LogMessage($"Error in CheckoutSteps_Before Method send email method called");          // This logs that Error in ValidateDocument Method send email method called
                LogMessageToFile($"Error in CheckoutSteps_Before Method send email method called");    // This logs that Error in ValidateDocument Method send email method called

                LogMessage($"Email sent successfully: {EmailErrorNotify.checkFlag}");                  // This logs Email sent successfully flag response 
                LogMessageToFile($"Email sent successfully: {EmailErrorNotify.checkFlag}");            // This logs Email sent successfully flag response 

                // Send the response validation is complete
                return eSuccess;

            }
        }

        #endregion


        #region |--Step 3: Call the method to set the handling charge to zero or to keep it the same--|

        // This method sets the handling charge
        public override int CalculateHandlingCharge(string orderId, out double handlingCharge, out string isoCurrencyCode)
        {

                // Log messages to the store and to the .txt file to show the storefronts original handling charge
                // I had to use these to figure out that my variables for the flags were not being passed between methods because they were not static
                if ((string)ModuleData[_EH_DEBUGGING_MODE] == "true")
                {
                // Log messages to the storefront "Logs" page
                LogMessage($"-----------------------------------------------------");
                LogMessage($"Step 3: Checking to See if We Need to Change Handling");
                LogMessage($"Storefront Order ID:             {orderId}");
                LogMessage($"Storefront Handling Charge:      {storeHandlingCharge}");
                LogMessage($"Extension Yes Flag:              {extensionYesFlag}");
                LogMessage($"Extension No Flag:               {extensionNoFlag}");                                    

                // Log messages to the .txt file
                LogMessageToFile($"-----------------------------------------------------");
                LogMessageToFile($"Step 3: Checking to See if We Need to Change Handling");
                LogMessageToFile($"Storefront Order ID:             {orderId}");
                LogMessageToFile($"Storefront Handling Charge:      {storeHandlingCharge}");
                LogMessageToFile($"Extension Yes Flag:              {extensionYesFlag}");
                LogMessageToFile($"Extension No Flag:               {extensionNoFlag}");
                }

            // Place in try catch to ensure we are alerted if something goes wrong
            try
            {
                // Check to see if the condition in which we will exclude handling is met
                // If the exclude handling Yes flag is set to Yes and the exclude handling No flag is set to No then we are good to exclude the handling
                if (extensionYesFlag.Equals("Yes") && extensionNoFlag.Equals("No"))
                {
                    // Set the excluded handling charge to be zero and return the same currency code we are required to return as well
                    handlingCharge = 0.00;
                    isoCurrencyCode = Storefront.GetValue(FieldType.SystemProperty, "IsoCurrencyCode", orderId);

                    // Log messages to the store and to the .txt file to show we have returned the isocurrentcycode correctly 
                    // and to tell that we have excluded the handling charge as well
                    if ((string)ModuleData[_EH_DEBUGGING_MODE] == "true")
                    {
                        // Log messages to the storefront "Logs" page
                        LogMessage($"Yes, We Need to Change the Handling Charge");
                        LogMessage($"V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V");
                        LogMessage($"-----------------------------------------------------");
                        LogMessage($"Show returned isoCurrencyCode:   {isoCurrencyCode}");
                        LogMessage($"The Handling Was Excluded from the Order");
                        LogMessage($"-----------------------------------------------------");
                        LogMessage($"V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V");

                        // Log messages to the .txt file
                        LogMessageToFile($"Yes, We Need to Change the Handling Charge");
                        LogMessageToFile($"V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V");
                        LogMessageToFile($"-----------------------------------------------------");
                        LogMessageToFile($"Show returned isoCurrencyCode:   {isoCurrencyCode}");
                        LogMessageToFile($"The Handling Was Excluded from the Order");
                        LogMessageToFile($"-----------------------------------------------------");
                        LogMessageToFile($"V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V");
                    }

                    // Send the new data back to the storefront for the handling charge
                    return eSuccess;
                }
                else
                {
                    // We set the values back to what was already on the store before we started
                    handlingCharge = storeHandlingCharge;
                    isoCurrencyCode = Storefront.GetValue(FieldType.SystemProperty, "IsoCurrencyCode", orderId);

                    // Log messages to the store and to the .txt file to show we have returned the isocurrentcycode correctly 
                    // and to tell that we have NOT excluded the handling charge as well
                    if ((string)ModuleData[_EH_DEBUGGING_MODE] == "true")
                    {
                        // Log messages to the storefront "Logs" page
                        LogMessage($"V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V");
                        LogMessage($"-----------------------------------------------------");
                        LogMessage($"Show returned isoCurrencyCode:   {isoCurrencyCode}");
                        LogMessage($"Handling Was Not Excluded");
                        LogMessage($"-----------------------------------------------------");
                        LogMessage($"V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V");

                        // Log messages to the .txt file
                        LogMessageToFile($"V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V");
                        LogMessageToFile($"-----------------------------------------------------");
                        LogMessageToFile($"Show returned isoCurrencyCode:   {isoCurrencyCode}");
                        LogMessageToFile($"Handling Was Not Excluded");
                        LogMessageToFile($"-----------------------------------------------------");
                        LogMessageToFile($"V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V^V");
                    }

                    // Send the new data back to the storefront for the handling charge
                    return eSuccess;
                }

            }
            catch
            {

                // We set the values back to what was already on the store before we started
                handlingCharge = storeHandlingCharge;
                isoCurrencyCode = Storefront.GetValue(FieldType.SystemProperty, "IsoCurrencyCode", orderId);

                // Log issue with storefront and to file regardless of whether in debug mode or not
                LogMessage("Error in CalculateHandlingCharge Method");                                 // This logs that there was an error in the CalculateHandlingCharge Method
                LogMessageToFile("Error in CalculateHandlingCharge Method");                           // This logs that there was an error in the CalculateHandlingCharge Method

                // Get the storefront's name from storefront and Date and time stamps as desired
                string sfName = SF.GetValue(FieldType.SystemProperty, SystemProperty.STOREFRONT_NAME, null);
                string currentLogDate = DateTime.Now.ToString("MMddyyyy");
                string currentLogTimeInsertMain = DateTime.Now.ToString("HH:mm:ss tt");

                //Setup our date and time for error
                string ErrorDate = string.Format("Date: {0}  Time: {1:G} <br>", currentLogDate, currentLogTimeInsertMain);

                // Setup our email body and message
                string subjectstring = "Storefront: \"" + sfName + "\" had an ERROR occur in the CalculateHandlingCharge Method";
                string bodystring = "Storefront: \"" + sfName + "\" had an ERROR occur in the CalculateHandlingCharge Method <br>" +
                                    ErrorDate +
                                    "Extension: Exclude Handling Extension <br>" +
                                    "ERROR occured with User ID: " + ErrorUserID + "<br>" +
                                    "ERROR occured with Order ID: " + ErrorOrderID + "<br>";

                // Call method to send our error as an email to developers maintaining sites
                EmailErrorNotify.CreateMessage(subjectstring, bodystring);

                // Log issue with storefront and to file regardless of whether in debug mode or not
                LogMessage($"Error in CalculateHandlingCharge Method send email method called");       // This logs that Error in CalculateHandlingCharge Method send email method called
                LogMessageToFile($"Error in CalculateHandlingCharge Method send email method called"); // This logs that Error in CalculateHandlingCharge Method send email method called

                LogMessage($"Email sent successfully: {EmailErrorNotify.checkFlag}");                  // This logs Email sent successfully flag response 
                LogMessageToFile($"Email sent successfully: {EmailErrorNotify.checkFlag}");            // This logs Email sent successfully flag response 

                // Send the response validation is complete
                return eSuccess;

            }
        }

        #endregion


    //end of the class: ExcludeHandling
    }
//end of the file
}
