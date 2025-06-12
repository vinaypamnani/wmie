using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;
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
    private readonly ISelectionService _selectionService;
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
    /// <param name="selectionService">The selection service.</param>
    public WmiInstanceViewModel(
        WmiInstance wmiInstance,
        WmiClassViewModel parentClass,
        IWmiService wmiService,
        IMessengerService messengerService,
        IApplicationService applicationService,
        ISelectionService selectionService) : base(messengerService)
    {
        if (wmiInstance == null) throw new ArgumentNullException(nameof(wmiInstance));
        if (parentClass == null) throw new ArgumentNullException(nameof(parentClass));
        if (wmiService == null) throw new ArgumentNullException(nameof(wmiService));
        if (messengerService == null) throw new ArgumentNullException(nameof(messengerService));
        if (applicationService == null) throw new ArgumentNullException(nameof(applicationService));
        if (selectionService == null) throw new ArgumentNullException(nameof(selectionService));

        _wmiInstance = wmiInstance;
        _wmiService = wmiService;
        _applicationService = applicationService;
        _parentClass = parentClass;
        _selectionService = selectionService;

        // Load instance methods
        LoadInstanceMethods();
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
    /// <param name="selectionService">The selection service.</param>
    /// <returns>A collection of WmiInstanceViewModel.</returns>
    public static ObservableCollection<WmiInstanceViewModel> CreateFromCollection(
        IEnumerable<WmiInstance> wmiInstances,
        IWmiService wmiService,
        IMessengerService messengerService,
        IApplicationService applicationService,
        ISelectionService selectionService,
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
                selectionService));
        }

        return viewModels;
    }

    /// <summary>
    /// Forces selection of this instance and publishes a selection message.
    /// </summary>
    public void ForceSelection()
    {
        // Update SelectionService with the current selection
        _selectionService.SetSelectedObject(this);
    }

    /// <summary>
    /// Returns a string representation of the instance.
    /// </summary>
    /// <returns>A string representation of the instance.</returns>
    public override string ToString() => _wmiInstance.ToString();

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
            System.Diagnostics.Debug.WriteLine($"[WmiInstanceViewModel] Error loading methods for instance {InstanceName}: {ex.Message}");
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

                // Update parent class selection to keep them in sync
                if (ParentClass.SelectedInstance != this)
                {
                    ParentClass.SelectedInstance = this;
                }

                // Ensure instance data is loaded
                TryGetInstance();

                // Notify selection service
                ForceSelection();
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
    }

    private void TryGetInstance()
    {
        try
        {
            // Attempt to load the instance data if not already loaded (useful for lazy props)
            WmiInstance.ActualObject?.Get();
            LoadState = InstanceState.Success;
        }
        catch
        {
            LoadState = InstanceState.Failed;
        }
    }
}