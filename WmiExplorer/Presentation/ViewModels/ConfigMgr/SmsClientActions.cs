namespace WmiExplorer.Presentation.ViewModels.ConfigMgr;

/// <summary>
/// Provides static group names for ConfigMgr Client Actions.
/// </summary>
public static class ClientActionGroup
{
    public const string ApplicationEvaluation = "Application Evaluation";
    public const string EndpointProtection = "Endpoint Protection";
    public const string Inventory = "Inventory";
    public const string LocationServices = "Location Services";
    public const string Other = "Other";
    public const string Policy = "Policy";
    public const string SoftwareUpdates = "Software Updates";
    public const string StateMessage = "State Message";
}

/// <summary>
/// Provides a static list of ConfigMgr Client Actions, grouped for menu display.
/// </summary>
public static class SmsClientActions
{
    public static readonly List<SmsClientAction> Actions = new()
    {
        new SmsClientAction("{00000000-0000-0000-0000-000000000001}", "Hardware Inventory Cycle", ClientActionGroup.Inventory),
        new SmsClientAction("{00000000-0000-0000-0000-000000000002}", "Software Inventory Cycle", ClientActionGroup.Inventory),
        new SmsClientAction("{00000000-0000-0000-0000-000000000003}", "Discovery Data Collection Cycle", ClientActionGroup.Inventory),
        new SmsClientAction("{00000000-0000-0000-0000-000000000010}", "File Collection Cycle", ClientActionGroup.Inventory),
        new SmsClientAction("{00000000-0000-0000-0000-000000000011}", "IDMIF Collection Cycle", ClientActionGroup.Inventory),
        new SmsClientAction("{00000000-0000-0000-0000-000000000012}", "Client Machine Authentication", ClientActionGroup.Other),
        new SmsClientAction("{00000000-0000-0000-0000-000000000021}", "Request Machine Assignments", ClientActionGroup.Policy),
        new SmsClientAction("{00000000-0000-0000-0000-000000000022}", "Evaluate Machine Assignments", ClientActionGroup.Policy),
        new SmsClientAction("{00000000-0000-0000-0000-000000000023}", "Refresh Default MP", ClientActionGroup.LocationServices),
        new SmsClientAction("{00000000-0000-0000-0000-000000000024}", "Refresh Locations", ClientActionGroup.LocationServices),
        new SmsClientAction("{00000000-0000-0000-0000-000000000025}", "Timeout Refresh", ClientActionGroup.LocationServices),
        new SmsClientAction("{00000000-0000-0000-0000-000000000026}", "Request User Assignments", ClientActionGroup.Policy),
        new SmsClientAction("{00000000-0000-0000-0000-000000000027}", "Evaluate User Assignments", ClientActionGroup.Policy),
        new SmsClientAction("{00000000-0000-0000-0000-000000000031}", "Software Metering Usage Report Cycle", ClientActionGroup.Inventory),
        new SmsClientAction("{00000000-0000-0000-0000-000000000032}", "Windows Installer Source List Update Cycle", ClientActionGroup.Other),
        new SmsClientAction("{00000000-0000-0000-0000-000000000037}", "Clear Proxy Settings Cache", ClientActionGroup.Other),
        new SmsClientAction("{00000000-0000-0000-0000-000000000040}", "Policy Agent Cleanup Cycle (Machine)", ClientActionGroup.Policy),
        new SmsClientAction("{00000000-0000-0000-0000-000000000041}", "Policy Agent Cleanup Cycle (User)", ClientActionGroup.Policy),
        new SmsClientAction("{00000000-0000-0000-0000-000000000042}", "Validate Machine Policy/Assignment", ClientActionGroup.Policy),
        new SmsClientAction("{00000000-0000-0000-0000-000000000043}", "Validate User Policy/Assignment", ClientActionGroup.Policy),
        new SmsClientAction("{00000000-0000-0000-0000-000000000051}", "Retry/Refresh Certificates in AD on MP", ClientActionGroup.Other),
        new SmsClientAction("{00000000-0000-0000-0000-000000000063}", "Software Updates Install Schedule", ClientActionGroup.SoftwareUpdates),
        new SmsClientAction("{00000000-0000-0000-0000-000000000071}", "Network Access Protection Schedule", ClientActionGroup.Other),
        new SmsClientAction("{00000000-0000-0000-0000-000000000108}", "Software Updates Assignment Evaluation Cycle", ClientActionGroup.SoftwareUpdates),
        new SmsClientAction("{00000000-0000-0000-0000-000000000110}", "DCM Policy", ClientActionGroup.Other),
        new SmsClientAction("{00000000-0000-0000-0000-000000000111}", "Send Unsent State Messages", ClientActionGroup.StateMessage),
        new SmsClientAction("{00000000-0000-0000-0000-000000000112}", "State System Policy Cache Clean", ClientActionGroup.StateMessage),
        new SmsClientAction("{00000000-0000-0000-0000-000000000113}", "Software Update Scan Cycle", ClientActionGroup.SoftwareUpdates),
        new SmsClientAction("{00000000-0000-0000-0000-000000000114}", "Software Update Store Refresh", ClientActionGroup.SoftwareUpdates),
        new SmsClientAction("{00000000-0000-0000-0000-000000000115}", "Bulk Send High Priority", ClientActionGroup.StateMessage),
        new SmsClientAction("{00000000-0000-0000-0000-000000000116}", "Bulk Send Low Priority", ClientActionGroup.StateMessage),
        new SmsClientAction("{00000000-0000-0000-0000-000000000120}", "AMT Status Check Policy", ClientActionGroup.Other),
        new SmsClientAction("{00000000-0000-0000-0000-000000000121}", "Application Manager Machine Policy", ClientActionGroup.ApplicationEvaluation),
        new SmsClientAction("{00000000-0000-0000-0000-000000000122}", "Application Manager User Policy", ClientActionGroup.ApplicationEvaluation),
        new SmsClientAction("{00000000-0000-0000-0000-000000000123}", "Application Manager Global Evaluation Policy", ClientActionGroup.ApplicationEvaluation),
        new SmsClientAction("{00000000-0000-0000-0000-000000000131}", "Power Management Summarizer", ClientActionGroup.Other),
        new SmsClientAction("{00000000-0000-0000-0000-000000000221}", "Endpoint Protection Deployment Re-Evaluate", ClientActionGroup.EndpointProtection),
        new SmsClientAction("{00000000-0000-0000-0000-000000000222}", "Endpoint Protection AM Policy Re-Evaluate", ClientActionGroup.EndpointProtection),

        // Excluded Actions:
        // {00000000-0000-0000-0000-000000000061}
        // {00000000-0000-0000-0000-000000000062}
        // {00000000-0000-0000-0000-000000000101}
        // {00000000-0000-0000-0000-000000000109}
        // {00000000-0000-0000-0000-000000000223}
    };
}