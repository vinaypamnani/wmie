using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Presentation.Views.Dialogs;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

/// <summary>
/// ViewModel for a WMI instance. Exposes instance properties and supports selection messaging.
/// </summary>
public partial class WmiInstanceViewModel : MessagingViewModelBase, IDisposable
{
    private readonly IApplicationService _applicationService;

    [ObservableProperty]
    private ObservableCollection<WmiMethod>? _instanceMethods;

    [ObservableProperty]
    private bool _isSelected;

    private bool _isUpdatingSelection = false;
    private readonly WmiClassViewModel _parentClass;
    private readonly SelectionManager _selectionManager;
    private readonly WmiInstance _wmiInstance;
    private readonly IWmiService _wmiService;

    [ObservableProperty]
    private ItemStatus itemStatus = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WmiInstanceViewModel"/> class.
    /// </summary>
    /// <param name="wmiInstance">The WMI instance model.</param>
    /// <param name="parentClass">The parent class ViewModel.</param>
    /// <param name="wmiService">The WMI service.</param>
    /// <param name="messenger">The messenger.</param>
    /// <param name="applicationService">The application service.</param>
    /// <param name="selectionManager">The selection service.</param>
    public WmiInstanceViewModel(
        WmiInstance wmiInstance,
        WmiClassViewModel parentClass,
        IWmiService wmiService,
        IMessengerService messengerService,
        IApplicationService applicationService,
        SelectionManager selectionManager) : base(messengerService)
    {
        if (wmiInstance == null) throw new ArgumentNullException(nameof(wmiInstance));
        if (parentClass == null) throw new ArgumentNullException(nameof(parentClass));
        if (wmiService == null) throw new ArgumentNullException(nameof(wmiService));
        if (messengerService == null) throw new ArgumentNullException(nameof(messengerService));
        if (applicationService == null) throw new ArgumentNullException(nameof(applicationService));
        if (selectionManager == null) throw new ArgumentNullException(nameof(selectionManager));

        _wmiInstance = wmiInstance;
        _wmiService = wmiService;
        _applicationService = applicationService;
        _parentClass = parentClass;
        _selectionManager = selectionManager;

        // Subscribe to ItemStatus property changes to notify Tooltip changes
        ItemStatus.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ItemStatus.LoadState) ||
                e.PropertyName == nameof(ItemStatus.StatusMessage) ||
                e.PropertyName == nameof(ItemStatus.Exception))
            {
                OnPropertyChanged(nameof(Tooltip));
            }
        };
    }

    /// <summary>
    /// The display name for this instance.
    /// </summary>
    public string InstanceName => _wmiInstance.InstanceName;

    /// <summary>
    /// The WMI path for this instance.
    /// </summary>
    public string NamespacePath => _wmiInstance.Path.Path;

    /// <summary>
    /// The parent class ViewModel.
    /// </summary>
    public WmiClassViewModel ParentClass => _parentClass;

    /// <summary>
    /// The parent namespace ViewModel.
    /// </summary>
    public WmiNamespaceViewModel? ParentNamespace => ParentClass.ParentNamespaceViewModel;

    public string? Tooltip
    {
        get
        {
            switch (ItemStatus.LoadState)
            {
                case LoadState.Unknown:
                    return null;
                case LoadState.Loading:
                    return "Loading";
                case LoadState.Success:
                    return "Success";
                case LoadState.PartialSuccess:
                    return "Instance has lazy properties. Refresh instance (F5) to load all properties.";
                case LoadState.Warning:
                    return !string.IsNullOrWhiteSpace(ItemStatus.StatusMessage) ? ItemStatus.StatusMessage : "Warning";
                case LoadState.Error:
                    return ItemStatus.Exception?.Message ?? "Failed";
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// The underlying ManagementObject for this instance.
    /// </summary>
    public WmiInstance WmiInstance => _wmiInstance;

    /// <summary>
    /// Creates a collection of WmiInstanceViewModel from a collection of WmiInstance models.
    /// </summary>
    /// <param name="wmiInstances">The collection of WMI instance models.</param>
    /// <param name="wmiService">The WMI service.</param>
    /// <param name="messenger">The messenger.</param>
    /// <param name="applicationService">The application service.</param>
    /// <param name="parentClass">The parent class ViewModel.</param>
    /// <param name="selectionManager">The selection service.</param>
    /// <returns>A collection of WmiInstanceViewModel.</returns>
    public static ObservableCollection<WmiInstanceViewModel> CreateFromCollection(
        IEnumerable<WmiInstance> wmiInstances,
        IWmiService wmiService,
        IMessengerService messengerService,
        IApplicationService applicationService,
        SelectionManager selectionManager,
        WmiClassViewModel parentClass)
    {
        if (wmiInstances == null)
            throw new ArgumentNullException(nameof(wmiInstances));

        var viewModels = new ObservableCollection<WmiInstanceViewModel>();

        foreach (var wmiInstance in wmiInstances)
        {
            viewModels.Add(new WmiInstanceViewModel(
                wmiInstance,
                parentClass,
                wmiService,
                messengerService,
                applicationService,
                selectionManager));
        }

        return viewModels;
    }

    /// <summary>
    /// Returns a string representation of the instance.
    /// </summary>
    /// <returns>A string representation of the instance.</returns>
    public override string ToString() => _wmiInstance.ToString();

    /// <summary>
    /// Command to copy the instance MOF to clipboard, with or without amended qualifiers.
    /// </summary>
    [RelayCommand]
    private void CopyInstanceMof(object? parameter = null)
    {
        bool useAmendedQualifiers = CommandParameterHelper.ParseBool(parameter, true);
        if (TryGetInstanceMof(useAmendedQualifiers, out var mof) && mof != null)
        {
            _applicationService.CopyToClipboard(mof);
            PublishSuccessState($"Instance MOF copied to clipboard (amended qualifiers: {useAmendedQualifiers})");
        }
    }

    /// <summary>
    /// Command to copy the instance path to clipboard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CopyRelativePathCanExecute))]
    private void CopyRelativePath()
    {
        // Copies the instance path to clipboard and notifies the user.
        if (string.IsNullOrEmpty(NamespacePath))
            return;

        _applicationService.CopyToClipboard(NamespacePath);
        PublishSuccessState($"Copied path: {NamespacePath}");
    }

    private bool CopyRelativePathCanExecute() => !string.IsNullOrEmpty(NamespacePath);

    /// <summary>
    /// Command to delete the instance.
    /// </summary>
    [RelayCommand]
    private async Task DeleteInstanceAsync()
    {
        var mainWindow = System.Windows.Application.Current.MainWindow;
        var result = MessageBoxDialog.Show(
            $"Are you sure you want to delete this instance?\n\n{InstanceName}",
            "Confirm Delete Instance",
            MessageBoxDialogButton.YesNo,
            MessageBoxDialogIcon.Question,
            mainWindow, false);

        if (result != MessageBoxDialogResult.Yes)
            return;

        try
        {
            await _wmiService.DeleteInstanceAsync(_wmiInstance.ActualObject!);
            // Remove from parent class collection
            _parentClass.RemoveInstance(this);
            PublishSuccessState($"Instance deleted: {InstanceName}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete instance: {InstanceName}", InstanceName);
            PublishErrorState($"Failed to delete instance: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Command to edit instance properties using PropertyEditorDialog.
    /// </summary>
    [RelayCommand(CanExecute = nameof(EditPropertiesCanExecute))]
    private void EditProperties()
    {
        try
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            var managementObject = _wmiInstance.ActualObject;

            if (managementObject != null)
            {
                // Show the PropertyEditorDialog
                var result = Presentation.Views.Dialogs.PropertyEditorDialog.ShowEditor(
                    mainWindow,
                    managementObject,
                    _messengerService,
                    $"Edit Instance: {_wmiInstance.ClassPath?.ClassName ?? "Unknown"}",
                    _wmiService,
                    true);

                if (result != null)
                {
                    // Debug: Log property values before saving
                    // LogPropertyValues("BEFORE PUT", _wmiInstance.ActualObject);
                    try
                    {

                        // Debug: Log property values after successful save
                        // LogPropertyValues("AFTER PUT", _wmiInstance.ActualObject);

                        // Refresh propertygrid
                        _selectionManager.PropertyGrid.RefreshPropertyGrid();

                        PublishSuccessState($"Properties updated for instance: {InstanceName}");
                        Log.Information("Properties updated for instance: {InstanceName}", InstanceName);
                    }
                    catch (Exception saveEx)
                    {
                        // Handle any other errors during save
                        Log.Error(saveEx, "Unexpected error saving changes for instance: {InstanceName}", InstanceName);
                        PublishErrorState($"Failed to save changes: {saveEx.Message}", saveEx);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error editing properties for instance: {InstanceName}", InstanceName);
            PublishErrorState($"Error editing instance properties: {ex.Message}", ex);
        }
    }

    private bool EditPropertiesCanExecute()
    {
        return _wmiInstance.ActualObject != null && _parentClass.HasWriteProperty;
    }

    /// <summary>
    /// Command to execute a WMI method.
    /// </summary>
    [RelayCommand(CanExecute = nameof(ExecuteMethodCanExecute))]
    private void ExecuteMethod(object? parameter)
    {
        if (parameter is WmiMethod method)
        {
            try
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;

                // Use the dialog to execute the method for instance methods
                if (ParentNamespace?.WmiNamespace != null)
                {
                    Presentation.Views.Dialogs.MethodExecutionDialog.ShowDialog(
                        mainWindow,
                        _wmiService,
                        ParentNamespace,
                        _parentClass.WmiClass,
                        method,
                        _messengerService,
                        _wmiInstance);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error showing method execution dialog for instance: {InstanceName}, method: {MethodName}",
                    InstanceName, method.Name);
                // Report error
                PublishErrorState($"Error showing executing method dialog: {ex.Message}", ex);
            }
        }
    }

    private bool ExecuteMethodCanExecute(object? parameter)
    {
        return parameter is WmiMethod &&
               ParentNamespace?.WmiNamespace != null;
    }

    /// <summary>
    /// Command to generate PowerShell script for this WMI instance.
    /// </summary>
    [RelayCommand]
    private void GenerateScript()
    {
        try
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            var managementScope = ParentNamespace?.ManagementScope;

            if (managementScope != null)
            {
                // Show the GenerateScriptDialog
                WmiExplorer.Presentation.Views.Dialogs.GenerateScriptDialog.ShowDialog(
                    mainWindow,
                    _wmiInstance,
                    managementScope);

                Log.Information("Generated PowerShell script for instance: {InstanceName}", InstanceName);
            }
            else
            {
                PublishErrorState("Cannot generate script: No namespace scope available.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating PowerShell script for instance: {InstanceName}", InstanceName);
            PublishErrorState($"Error generating PowerShell script: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads the methods available for this instance from the parent class.
    /// </summary>
    private void LoadInstanceMethods()
    {
        InstanceMethods = new ObservableCollection<WmiMethod>();

        try
        {
            // Get the methods from the parent class's WmiClass
            var methods = _parentClass.WmiClass.Methods;

            if (methods != null && methods.Count > 0)
            {
                foreach (var method in methods)
                {
                    // Only add non-static methods to instance methods
                    if (!method.IsStatic)
                    {
                        // Add each method to the collection
                        InstanceMethods.Add(method);
                    }
                }
            }

            // Notify that command can execute state may have changed
            ExecuteMethodCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading methods for instance: {InstanceName}", InstanceName);
        }
    }

    /// <summary>
    /// Debug method to log property values of a ManagementBaseObject.
    /// </summary>
    private void LogPropertyValues(string phase, System.Management.ManagementBaseObject obj)
    {
        try
        {
            Log.Debug("=== {Phase} - Property Values for {ClassName} ===", phase, obj.ClassPath?.ClassName ?? "Unknown");

            foreach (System.Management.PropertyData prop in obj.Properties)
            {
                try
                {
                    var value = prop.Value?.ToString() ?? "null";
                    var type = prop.Type.ToString();
                    Log.Debug("  {PropertyName} ({Type}): '{Value}'", prop.Name, type, value);
                }
                catch (Exception ex)
                {
                    Log.Debug("  {PropertyName}: Error reading value - {Error}", prop.Name, ex.Message);
                }
            }

            Log.Debug("=== End {Phase} ===", phase);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error logging property values during {Phase}", phase);
        }
    }

    partial void OnInstanceMethodsChanged(ObservableCollection<WmiMethod>? value)
    {
        ExecuteMethodCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (_isUpdatingSelection) return;

        if (value)
        {
            try
            {
                _isUpdatingSelection = true;

                // Load Instance Methods
                LoadInstanceMethods();

                // Set state based on parent class properties
                if (ItemStatus.LoadState == LoadState.Unknown)
                {
                    if (!_parentClass.HasLazyProperty)
                        SetStatusAndPublish(ItemStatus, LoadState.Success, $"Showing details for {InstanceName}.");
                    else
                        SetStatusAndPublish(ItemStatus, LoadState.PartialSuccess, $"Showing details for {InstanceName} (with lazy properties).");
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
    }

    /// <summary>
    /// Command to refresh the instance and update the property grid.
    /// </summary>
    [RelayCommand]
    private void RefreshInstance()
    {
        try
        {
            // Always reload the instance data
            _wmiService.RefreshInstanceAsync(_wmiInstance.ActualObject);
            SetStatusAndPublish(ItemStatus, LoadState.Success, $"Instance refreshed: {InstanceName}");

            // Refresh the property grid to reflect updated values
            _selectionManager.PropertyGrid.RefreshPropertyGrid();

            Log.Information("Instance refreshed: {InstanceName}", InstanceName);
        }
        catch (Exception ex)
        {
            SetStatusAndPublish(ItemStatus, LoadState.Error, $"Failed to refresh instance: {ex.Message}", ex);
            Log.Error(ex, "Failed to refresh instance: {InstanceName}", InstanceName);
        }
    }

    /// <summary>
    /// Command to show the instance MOF in a dialog, with or without amended qualifiers.
    /// </summary>
    [RelayCommand]
    private void ShowInstanceMof(object? parameter = null)
    {
        bool useAmendedQualifiers = CommandParameterHelper.ParseBool(parameter, true);
        if (TryGetInstanceMof(useAmendedQualifiers, out var mof) && mof != null)
        {
            var dialog = new WmiExplorer.Presentation.Views.Dialogs.MofViewerDialog(mof)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }
    }

    /// <summary>
    /// Retrieves the MOF representation of the instance, handling UseAmendedQualifiers and error reporting.
    /// </summary>
    /// <param name="useAmendedQualifiers">Whether to use amended qualifiers.</param>
    /// <param name="mof">The resulting MOF string, or null if failed.</param>
    /// <returns>True if successful, false otherwise.</returns>
    private bool TryGetInstanceMof(bool useAmendedQualifiers, out string? mof)
    {
        mof = null;
        try
        {
            var managementObject = _wmiInstance.ActualObject;
            if (managementObject == null)
            {
                PublishErrorState("Instance data is not loaded.");
                return false;
            }

            // Store the original value to restore after operation
            bool originalValue = managementObject.Options.UseAmendedQualifiers;
            managementObject.Options.UseAmendedQualifiers = useAmendedQualifiers;

            // Get the MOF representation of the instance
            _wmiService.RefreshInstanceAsync(managementObject);
            mof = managementObject.GetText(System.Management.TextFormat.Mof);

            // Restore the original value
            managementObject.Options.UseAmendedQualifiers = originalValue;
            _wmiService.RefreshInstanceAsync(managementObject);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get instance MOF for: {InstanceName}", InstanceName);
            PublishErrorState($"Failed to get instance MOF: {ex.Message}", ex);
            return false;
        }
    }

    #region IDisposable

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (InstanceMethods != null)
            {
                foreach (var method in InstanceMethods)
                {
                    if (method is IDisposable disposable)
                    {
                        try { disposable.Dispose(); } catch { }
                    }
                }
                InstanceMethods.Clear();
            }
            _wmiInstance?.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}