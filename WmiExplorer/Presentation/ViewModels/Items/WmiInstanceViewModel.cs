using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

/// <summary>
/// ViewModel for a WMI instance. Exposes instance properties and supports selection messaging.
/// </summary>
public partial class WmiInstanceViewModel : MessagingViewModelBase, IDisposable
{
    public enum InstanceState
    {
        Unknown,
        Success,
        Failed
    }

    private readonly IApplicationService _applicationService;

    [ObservableProperty]
    private ObservableCollection<WmiMethod>? _instanceMethods;

    [ObservableProperty]
    private bool _isSelected;

    private bool _isUpdatingSelection = false;

    [ObservableProperty]
    private InstanceState _loadState = InstanceState.Unknown;

    private readonly WmiClassViewModel _parentClass;
    private readonly SelectionManager _selectionManager;
    private readonly WmiInstance _wmiInstance;
    private readonly IWmiService _wmiService;

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

    public void TryGetInstance()
    {
        try
        {

            if (LoadState == InstanceState.Unknown)
            {
                // Attempt to load the instance data if not already loaded (useful for lazy props)
                WmiInstance.ActualObject?.Get();
                LoadState = InstanceState.Success;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load instance data for: {InstanceName}", InstanceName);
            LoadState = InstanceState.Failed;
        }
    }

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
                    $"Edit Instance: {_wmiInstance.ClassPath?.ClassName ?? "Unknown"}");

                if (result != null)
                {
                    // Debug: Log property values before saving
                    // LogPropertyValues("BEFORE PUT", _wmiInstance.ActualObject);

                    // Validate the property changes first by attempting to save
                    try
                    {
                        // Use PutOptions to be explicit about update behavior
                        var putOptions = new System.Management.PutOptions
                        {
                            Type = System.Management.PutType.UpdateOnly // Only update existing instance
                        };

                        // Save changes to the instance
                        var putPath = _wmiInstance.ActualObject.Put(putOptions);

                        // Debug: Log property values after successful save
                        // LogPropertyValues("AFTER PUT", _wmiInstance.ActualObject);

                        // Refresh the instance data
                        TryGetInstance();

                        // Refresh propertygrid
                        _selectionManager.PropertyGrid.RefreshPropertyGrid();

                        PublishSuccessState($"Properties updated for instance: {InstanceName}");
                        Log.Information("Properties updated for instance: {InstanceName}", InstanceName);
                    }
                    catch (System.Management.ManagementException mgmtEx)
                    {
                        // Handle WMI-specific errors with detailed messages
                        string errorMessage = GetDetailedManagementErrorMessage(mgmtEx);
                        Log.Error(mgmtEx, "WMI error updating properties for instance: {InstanceName}. Error: {ErrorCode}", InstanceName, mgmtEx.ErrorCode);
                        PublishErrorState($"Failed to update instance properties: {errorMessage}", mgmtEx);
                    }
                    catch (System.ArgumentException argEx)
                    {
                        // Handle validation errors (like invalid Char16 values)
                        Log.Error(argEx, "Property validation error for instance: {InstanceName}", InstanceName);
                        PublishErrorState($"Property validation failed: {argEx.Message}", argEx);
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
                        ParentNamespace.WmiNamespace,
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
    /// Converts ManagementException error codes to user-friendly messages.
    /// </summary>
    private string GetDetailedManagementErrorMessage(System.Management.ManagementException mgmtEx)
    {
        return mgmtEx.ErrorCode switch
        {
            System.Management.ManagementStatus.InvalidParameter => "One or more property values are invalid. Please check the data types and value ranges.",
            System.Management.ManagementStatus.TypeMismatch => "Property value type mismatch. Please ensure the value matches the expected data type.",
            System.Management.ManagementStatus.ValueOutOfRange => "Property value is out of the allowed range.",
            System.Management.ManagementStatus.InvalidPropertyType => "Invalid property type specified.",
            System.Management.ManagementStatus.InvalidCimType => "Invalid CIM type for property value.",
            System.Management.ManagementStatus.IllegalNull => "Property cannot be null. Please provide a valid value.",
            System.Management.ManagementStatus.ReadOnly => "One or more properties are read-only and cannot be modified.",
            System.Management.ManagementStatus.AccessDenied => "Access denied. You don't have permission to modify this instance.",
            System.Management.ManagementStatus.NotFound => "Instance not found. It may have been deleted by another process.",
            System.Management.ManagementStatus.InvalidObject => "The instance object is invalid or corrupted.",
            _ => $"WMI Error ({mgmtEx.ErrorCode}): {mgmtEx.Message}"
        };
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

                // Force the instance to try to get its data
                TryGetInstance();
            }
            finally
            {
                _isUpdatingSelection = false;
            }
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
            managementObject.Get();
            mof = managementObject.GetText(System.Management.TextFormat.Mof);

            // Restore the original value
            managementObject.Options.UseAmendedQualifiers = originalValue;
            managementObject.Get();

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