using System.Collections.ObjectModel;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

/// <summary>
/// ViewModel for a WMI instance. Exposes instance properties and supports selection messaging.
/// </summary>
public class WmiInstanceViewModel : MessagingViewModelBase
{
    public enum InstanceLoadState
    {
        Unknown,
        Success,
        Failed
    }

    private readonly IApplicationService _applicationService;
    private ObservableCollection<WmiMethod>? _instanceMethods;
    private InstanceLoadState _loadState = InstanceLoadState.Unknown;
    private readonly WmiClassViewModel _parentClass;
    private readonly WmiInstance _wmiInstance;
    private readonly IWmiService _wmiService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WmiInstanceViewModel"/> class.
    /// </summary>
    /// <param name="wmiInstance">The WMI instance model.</param>
    /// <param name="parentClass">The parent class ViewModel.</param>
    /// <param name="wmiService">The WMI service.</param>
    /// <param name="messagingService">The messaging service.</param>
    /// <param name="applicationService">The application service.</param>
    public WmiInstanceViewModel(
        WmiInstance wmiInstance,
        WmiClassViewModel parentClass,
        IWmiService wmiService,
        IMessagingService messagingService,
        IApplicationService applicationService)
    {
        if (wmiInstance == null) throw new ArgumentNullException(nameof(wmiInstance));
        if (parentClass == null) throw new ArgumentNullException(nameof(parentClass));
        if (wmiService == null) throw new ArgumentNullException(nameof(wmiService));
        if (messagingService == null) throw new ArgumentNullException(nameof(messagingService));
        if (applicationService == null) throw new ArgumentNullException(nameof(applicationService));

        _wmiInstance = wmiInstance;
        _wmiService = wmiService;
        _applicationService = applicationService;
        _parentClass = parentClass;

        InitializeMessaging(messagingService);

        // Initialize commands
        CopyRelativePathCommand = new RelayCommand(CopyRelativePath);
        ExecuteMethodCommand = new RelayCommand(ExecuteMethod);

        // Load instance methods
        LoadInstanceMethods();
    }

    /// <summary>
    /// Command to copy the instance path to clipboard.
    /// </summary>
    public ICommand CopyRelativePathCommand { get; }

    /// <summary>
    /// Command to execute a WMI method.
    /// </summary>
    public ICommand ExecuteMethodCommand { get; }

    /// <summary>
    /// Collection of methods available for this instance.
    /// </summary>
    public ObservableCollection<WmiMethod> InstanceMethods => _instanceMethods!;

    /// <summary>
    /// The display name for this instance.
    /// </summary>
    public string InstanceName => _wmiInstance.InstanceName;

    /// <summary>
    /// Load state for the indicator. Success if ever selected, Unknown otherwise.
    /// </summary>
    public InstanceLoadState LoadState
    {
        get => _loadState;
        set => SetProperty(ref _loadState, value);
    }

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
    /// <param name="messagingService">The messaging service.</param>
    /// <param name="applicationService">The application service.</param>
    /// <param name="parentClass">The parent class ViewModel.</param>
    /// <returns>A collection of WmiInstanceViewModel.</returns>
    public static ObservableCollection<WmiInstanceViewModel> CreateFromCollection(
        IEnumerable<WmiInstance> wmiInstances,
        IWmiService wmiService,
        IMessagingService messagingService,
        IApplicationService applicationService,
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
                messagingService,
                applicationService));
        }

        return viewModels;
    }

    /// <summary>
    /// Forces selection of this instance and publishes a selection message.
    /// </summary>
    public void ForceSelection()
    {
        // Always publish the message even if already selected (for UI refresh scenarios).
        PublishMessage(new SelectedInstanceChangedMessage(this));
    }

    /// <summary>
    /// Returns a string representation of the instance.
    /// </summary>
    /// <returns>A string representation of the instance.</returns>
    public override string ToString() => _wmiInstance.ToString();

    private void CopyRelativePath(object? parameter)
    {
        // Copies the instance path to clipboard and notifies the user.
        if (string.IsNullOrEmpty(NamespacePath))
            return;

        _applicationService.CopyToClipboard(NamespacePath);
        PublishSuccessState($"Copied path: {NamespacePath}");
    }

    /// <summary>
    /// Executes a WMI method from the context menu.
    /// </summary>
    /// <param name="parameter">The WmiMethod to execute.</param>
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

    /// <summary>
    /// Loads the methods available for this instance from the parent class.
    /// </summary>
    private void LoadInstanceMethods()
    {
        _instanceMethods = new ObservableCollection<WmiMethod>();

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
                        _instanceMethods.Add(method);
                    }
                }
            }

            // Log the number of methods found
            // System.Diagnostics.Debug.WriteLine($"Loaded {_instanceMethods.Count} methods for instance {InstanceName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading methods for instance {InstanceName}: {ex.Message}");
        }
    }
}