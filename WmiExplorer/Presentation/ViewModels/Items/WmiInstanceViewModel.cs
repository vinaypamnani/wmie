using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

/// <summary>
/// ViewModel for a WMI instance. Exposes instance properties and supports selection messaging.
/// </summary>
public partial class WmiInstanceViewModel : MessagingViewModelBase
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
            Log.Warning(ex, "Failed to load instance data for: {InstanceName}", InstanceName);
            LoadState = InstanceState.Failed;
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
                    $"Edit {InstanceName}"); if (result != null)
                {
                    // Save changes to the instance
                    _wmiInstance.ActualObject.Put();

                    // Refresh the instance data
                    TryGetInstance();

                    // Refresh propertygrid
                    _selectionManager.RefreshPropertyGrid();

                    PublishSuccessState($"Properties updated for instance: {InstanceName}");
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
        return _wmiInstance.ActualObject != null;
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
            Log.Warning(ex, "Error loading methods for instance: {InstanceName}", InstanceName);
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
}