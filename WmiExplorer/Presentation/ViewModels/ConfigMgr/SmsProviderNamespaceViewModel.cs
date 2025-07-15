using WmiExplorer.Common.Models;
using WmiExplorer.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.ConfigMgr;

/// <summary>
/// ViewModel for SMS Provider namespaces, isolates ConfigMgr-specific logic.
/// </summary>
public class SmsProviderNamespaceViewModel : WmiNamespaceViewModel
{
    public SmsProviderNamespaceViewModel(
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
    }

    /// <summary>
    /// Always true for this derived type.
    /// </summary>
    public override bool IsSmsProviderNamespace => true;

    /// <summary>
    /// Builds a WQL query string for SMS Provider namespaces, applying additional filters for collections and inventory classes.
    /// </summary>
    public static string BuildSmsProviderQueryFromFilter(string baseQuery, ConfigMgrSettings configMgrSettings)
    {
        var queryString = baseQuery;

        // Exclude collection classes if not included
        if (!configMgrSettings.IncludeCollectionClasses)
        {
            queryString += " AND NOT __Class LIKE \"SMS_CM_RES_COLL_%\"";
        }

        // Exclude inventory classes if not included
        if (!configMgrSettings.IncludeInventoryClasses)
        {
            queryString += " AND NOT __Class LIKE \"SMS_G_System%\"";
            queryString += " AND NOT __Class LIKE \"SMS_GH_System%\"";
            queryString += " AND NOT __Class LIKE \"SMS_GEH_System%\"";
        }

        return queryString;
    }

    /// <summary>
    /// Helper to detect SMS Provider namespace path (ROOT\SMS\SITE_XXX where XXX is alphanumeric).
    /// </summary>
    public static bool IsSmsProviderNamespacePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;
        // Match pattern root\sms\site_XXX (case-insensitive, XXX = alphanumeric)
        return System.Text.RegularExpressions.Regex.IsMatch(
            relativePath.Trim(),
            @"^root\\sms\\site_[a-zA-Z0-9]+$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}