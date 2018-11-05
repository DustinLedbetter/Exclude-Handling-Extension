# Exclude-Handling-Extension
This extension excludes the handling of certain items in the storefront when checking out.

This extension checks items in the cart at the time that the user begins checkout to see if there should be a handling charge or not based on the items in the cart. 

*(This version has been updated to add logging features and has added commenting for use in debugging )*

*(Added feature that sends out emails when errors occur in the extension)*

0.    Variable fields
1.    DisplayName()
2.    UniqueName()
3.    PARAMS_WE_WANT
4.    private ISINI GetSf () (reduces code throughout project)
5.    LogMessageToFile
6.    GetConfigurationHtml
7.    IsModuleType (string x) (determines if at shipping step)
8.    ValidateDocument()
9.    CheckoutSteps_Before()
10.   CalculateHandlingCharge()
