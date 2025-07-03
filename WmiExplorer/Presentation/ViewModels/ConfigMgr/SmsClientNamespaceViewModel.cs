using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using System.Windows.Input;
using WmiExplorer.Common.Logging;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.ConfigMgr;

/// <summary>
/// ViewModel for SMS Client namespaces (root\CCM), isolates client-specific logic.
/// </summary>
public partial class SmsClientNamespaceViewModel : WmiNamespaceViewModel
{
    private const string SmsClientClassName = "SMS_Client";
    private const string SmsClientNamespacePathConst = "ROOT\\CCM";

    [ObservableProperty]
    private bool _isSmsClientInstalled;

    [ObservableProperty]
    private ManagementClass? _smsClientClass;

    private readonly IWmiService _wmiService;

    public SmsClientNamespaceViewModel(
        WmiNamespace wmiNamespace,
        IWmiService wmiService,
        IMessengerService messengerService,
        IApplicationService applicationService,
        SettingsManager settingsManager,
        ICacheService cacheService,
        SelectionManager selectionManager,
        WmiNamespaceViewModel? parentNamespaceViewModel = null)
        : base(wmiNamespace, wmiService, messengerService, applicationService, settingsManager, cacheService, selectionManager, parentNamespaceViewModel)
    {
        _wmiService = wmiService;
        _ = InitializeSmsClientAsync();

        TriggerClientActionCommand = new RelayCommand<SmsClientAction>(TriggerClientAction);
    }

    /// <summary>
    /// Exposes grouped ConfigMgr Client Actions for context menu binding.
    /// </summary>
    public ObservableCollection<IGrouping<string, SmsClientAction>> GroupedClientActions { get; } =
        new ObservableCollection<IGrouping<string, SmsClientAction>>(
            SmsClientActions.Actions.GroupBy(a => a.Group).OrderBy(g => g.Key)
        );

    /// <summary>
    /// Always true for this derived type.
    /// </summary>
    public override bool IsSmsClientNamespace => IsSmsClientInstalled;

    /// <summary>
    /// Command to trigger a ConfigMgr Client Action.
    /// </summary>
    public ICommand TriggerClientActionCommand { get; }

    /// <summary>
    /// Helper to detect SMS Client namespace path (root\CCM).
    /// </summary>
    public static bool IsSmsClientNamespacePath(string? relativePath)
    {
        return relativePath != null && relativePath.Trim().Equals(SmsClientNamespacePathConst, StringComparison.OrdinalIgnoreCase);
    }

    private async Task InitializeSmsClientAsync()
    {
        Log.Debug($"Checking for {SmsClientClassName} class in namespace: {WmiNamespace?.NamespacePath}");
        if (WmiNamespace == null)
        {
            SmsClientClass = null;
            IsSmsClientInstalled = false;
            Log.Warning("WmiNamespace is null. SMS Client will be marked as not installed.");
            return;
        }

        var smsClientClass = await _wmiService.TryGetManagementClassAsync(WmiNamespace.NamespacePath, WmiNamespace.ConnectionOptions, SmsClientClassName, CancellationToken.None);
        SmsClientClass = smsClientClass;
        IsSmsClientInstalled = smsClientClass != null;
        Log.Information($"{SmsClientClassName} class {(smsClientClass != null ? "found" : "not found")} in namespace: {WmiNamespace.NamespacePath}");
    }

    /// <summary>
    /// Triggers the selected ConfigMgr Client Action.
    /// </summary>
    private async void TriggerClientAction(SmsClientAction? action)
    {
        if (action == null)
        {
            Log.Warning("TriggerClientAction called with null action.");
            return;
        }
        if (SmsClientClass == null)
        {
            Log.Warning("TriggerClientAction called but SmsClientClass is null.");
            return;
        }
        try
        {
            Log.Debug($"Triggering ConfigMgr Client Action '{action.DisplayName}' with ID: {action.Id}");
            PublishBusyState($"Triggering ConfigMgr Client Action '{action.DisplayName}' with ID: {action.Id}");

            // Prepare input parameters for TriggerSchedule
            var inParams = SmsClientClass.GetMethodParameters("TriggerSchedule");
            inParams["sScheduleID"] = action.Id;

            // Execute the TriggerSchedule method using the WmiService
            var result = await _wmiService.ExecuteClassMethodAsync(SmsClientClass, "TriggerSchedule", inParams);

            if (result != null)
            {
                Log.Information($"Successfully triggered action: {action.DisplayName}");
                PublishSuccessState($"Successfully triggered action: {action.DisplayName}");
            }
            else
            {
                Log.Error($"Failed to trigger action: {action.DisplayName}. Output was null.");
                PublishErrorState($"Failed to trigger action: {action.DisplayName}. Output was null.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to trigger action: {action?.DisplayName}");
            PublishErrorState($"Exception while triggering action: {action?.DisplayName}");
        }
    }
}