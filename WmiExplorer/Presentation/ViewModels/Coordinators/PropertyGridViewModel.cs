using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the PropertyGrid functionality.
/// Manages property grid operations and selected object display.
/// </summary>
public class PropertyGridViewModel : MessagingViewModelBase
{
    private readonly ISettingsService _settingsService;
    private object? _selectedObject;
    private MainWindowPosition _windowPosition;

    public PropertyGridViewModel(
        IMessagingService messagingService,
        ISettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Initialize messaging
        InitializeMessaging(messagingService);

        // Initialize window position from settings (following the pattern)
        _windowPosition = _settingsService.MainWindowPosition;

        // Subscribe to selection change messages
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
        StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);
        StrongSubscribe<SelectedInstanceChangedMessage>(HandleSelectedInstanceChangedMessage);
        StrongSubscribe<SelectedEventChangedMessage>(HandleSelectedEventChangedMessage);
        StrongSubscribe<SelectedSearchResultChangedMessage>(HandleSelectedSearchResultChangedMessage);
        StrongSubscribe<WmiQueryInstanceChangedMessage>(HandleWmiQueryInstanceChangedMessage);
    }

    /// <summary>
    /// Object to display in the property grid - could be namespace, class, or instance
    /// </summary>
    public object? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (SetProperty(ref _selectedObject, value))
            {
                // Notify that the display name has changed when the selected object changes
                OnPropertyChanged(nameof(SelectedObjectDisplayName));
            }
        }
    }

    /// <summary>
    /// Gets the display name of the currently selected object for the property grid header
    /// </summary>
    public string SelectedObjectDisplayName
    {
        get
        {
            if (_selectedObject == null)
                return "No Selection";

            if (_selectedObject is WmiNamespaceViewModel namespaceVm)
                return $"Namespace: {namespaceVm.Name}";

            if (_selectedObject is WmiClassViewModel classVm)
                return $"Class: {classVm.ClassName}";

            if (_selectedObject is WmiInstanceViewModel instanceVm)
                return $"Instance: {instanceVm.InstanceName}";

            return _selectedObject.GetType().Name;
        }
    }

    /// <summary>
    /// Gets the window position settings for PropertyGrid-related properties
    /// </summary>
    public MainWindowPosition WindowPosition
    {
        get => _windowPosition;
        set => SetProperty(ref _windowPosition, value);
    }

    /// <summary>
    /// Handles when a class is selected to update the property grid
    /// </summary>
    private void HandleSelectedClassChangedMessage(SelectedClassChangedMessage message)
    {
        if (message?.ClassViewModel == null)
            return;

        // Update the selected object for the property grid
        SelectedObject = message.ClassViewModel.WmiClass;
    }

    /// <summary>
    /// Handles when a WMI event is selected to update the property grid
    /// </summary>
    private void HandleSelectedEventChangedMessage(SelectedEventChangedMessage message)
    {
        if (message?.WmiEvent == null)
            return;

        // Update the selected object for the property grid
        SelectedObject = message.WmiEvent;
    }

    /// <summary>
    /// Handles when an instance is selected to update the property grid
    /// </summary>
    private void HandleSelectedInstanceChangedMessage(SelectedInstanceChangedMessage message)
    {
        if (message?.InstanceViewModel == null)
            return;

        // Update the selected object for the property grid
        SelectedObject = message.InstanceViewModel.WmiInstance;
    }

    /// <summary>
    /// Handles when a namespace is selected to update the property grid
    /// </summary>
    private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
    {
        if (message?.NamespaceViewModel == null)
            return;

        // Update the selected object for the property grid
        SelectedObject = message.NamespaceViewModel.WmiNamespace;
    }

    /// <summary>
    /// Handles when a search result is selected to update the property grid
    /// </summary>
    private void HandleSelectedSearchResultChangedMessage(SelectedSearchResultChangedMessage message)
    {
        // Set SelectedObject to the underlying WMI object for the property grid
        if (message?.SelectedResult != null)
        {
            if (message.SelectedResult.Class != null)
                SelectedObject = message.SelectedResult.Class;
            else if (message.SelectedResult.Method != null)
                SelectedObject = message.SelectedResult.Method;
            else if (message.SelectedResult.Property != null)
                SelectedObject = message.SelectedResult.Property;
            else
                SelectedObject = message.SelectedResult.Match;
        }
        else
        {
            SelectedObject = null;
        }
    }

    /// <summary>
    /// Handles when a WMI query result instance is selected to update the property grid
    /// </summary>
    private void HandleWmiQueryInstanceChangedMessage(WmiQueryInstanceChangedMessage message)
    {
        if (message?.Instance == null)
            return;
            
        // Set SelectedObject to the selected WMI instance for the property grid
        SelectedObject = message.Instance;
    }
}
