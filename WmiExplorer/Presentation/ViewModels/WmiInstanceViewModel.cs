using System.Collections.ObjectModel;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels;

/// <summary>
/// ViewModel for a WMI instance. Exposes instance properties and supports selection messaging.
/// </summary>
public class WmiInstanceViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;
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

        CopyRelativePathCommand = new RelayCommand(CopyRelativePath);
    }

    /// <summary>
    /// Command to copy the instance path to clipboard.
    /// </summary>
    public ICommand CopyRelativePathCommand { get; }

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
}