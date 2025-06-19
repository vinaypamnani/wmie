using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Properties tab. Manages property-related functionality
/// and UI operations for the properties list view.
/// </summary>
public partial class PropertiesTabViewModel : MessagingViewModelBase
{
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _helpText = "Select a class to view properties";

    private FilterHelper<WmiProperty>? _propertyFilterHelper;

    [ObservableProperty]
    private string _propertyFilterText = string.Empty;

    [ObservableProperty]
    private WmiClassViewModel? _selectedClass;

    [ObservableProperty]
    private WmiProperty? _selectedProperty;

    private readonly ISelectionService _selectionService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    private readonly IWmiService _wmiService;

    public PropertiesTabViewModel(
           IMessengerService messengerService,
           ISettingsService settingsService,
           ISelectionService selectionService,
           IWmiService wmiService) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));

        // Subscribe to unified selection changes
        StrongSubscribe<SelectionChangedMessage>(HandleSelectionChangedMessage);

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;
    }

    /// <summary>
    /// Gets the filtered view of properties for the selected class.
    /// </summary>
    public ICollectionView? FilteredPropertiesView => _propertyFilterHelper?.CollectionView;

    /// <summary>
    /// Cleanup resources on disposal
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
            _propertyFilterHelper?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Handles the unified selection changed message
    /// </summary>
    private void HandleSelectionChangedMessage(SelectionChangedMessage message)
    {
        if (message?.SelectionService == null)
            return;

        var selectedObject = message.SelectionService.SelectedObject;

        switch (selectedObject)
        {
            // If a namespace is selected, update class selection
            case WmiNamespaceViewModel namespaceVm:
                if (namespaceVm.SelectedClass != SelectedClass)
                {
                    SelectedClass = namespaceVm.SelectedClass;
                }
                break;

            // If a class is selected, update the selected class
            case WmiClassViewModel classVm:
                if (classVm != SelectedClass)
                {
                    SelectedClass = classVm;
                }
                break;

            // If a property is selected, update the selected property
            case WmiProperty propertyVm:
                if (propertyVm != SelectedProperty)
                {
                    SelectedProperty = propertyVm;
                }
                break;
        }
    }

    /// <summary>
    /// Called when the PropertyFilterText changes
    /// </summary>
    partial void OnPropertyFilterTextChanged(string value)
    {
        if (_propertyFilterHelper != null)
        {
            _propertyFilterHelper.FilterText = value;
        }
    }

    /// <summary>
    /// Called when the selected class changes to reset the selected property
    /// </summary>
    partial void OnSelectedClassChanged(WmiClassViewModel? oldValue, WmiClassViewModel? newValue)
    {
        // Reset selected property when class changes
        SelectedProperty = null;

        // Dispose of the old filter helper
        _propertyFilterHelper?.Dispose();
        _propertyFilterHelper = null;        // Create new filter helper if we have properties
        if (newValue?.WmiClass?.Properties != null)
        {
            // Convert PropertyDataCollection to WmiProperty collection
            var wmiProperties = new ObservableCollection<WmiProperty>();
            foreach (System.Management.PropertyData propData in newValue.WmiClass.Properties)
            {
                wmiProperties.Add(new WmiProperty(propData, newValue.WmiClass.ActualClass));
            }

            _propertyFilterHelper = new FilterHelper<WmiProperty>(
                wmiProperties,
                PropertyFilterPredicate
            );

            // Apply current filter text if any
            if (!string.IsNullOrWhiteSpace(PropertyFilterText))
            {
                _propertyFilterHelper.FilterText = PropertyFilterText;
            }
        }

        // Notify UI that FilteredPropertiesView has changed
        OnPropertyChanged(nameof(FilteredPropertiesView));

        // Update help text based on class selection
        UpdateHelpText();

        // Update status bar
        if (newValue != null)
        {
            var totalPropertyCount = newValue.WmiClass?.Properties?.Count ?? 0;
            var keyPropertyCount = 0; if (newValue.WmiClass?.Properties != null)
            {
                foreach (System.Management.PropertyData propData in newValue.WmiClass.Properties)
                {
                    var wmiProp = new WmiProperty(propData, newValue.WmiClass.ActualClass);
                    if (wmiProp.IsKey)
                        keyPropertyCount++;
                }
            }

            if (totalPropertyCount > 0)
            {
                PublishSuccessState($"Found {totalPropertyCount} properties ({keyPropertyCount} key properties) for class {newValue.ClassName}");
            }
            else
            {
                PublishWarningState($"No properties available for class {newValue.ClassName}");
            }
        }
    }

    /// <summary>
    /// Called when the selected property changes
    /// </summary>
    partial void OnSelectedPropertyChanged(WmiProperty? oldValue, WmiProperty? newValue)
    {
        UpdateHelpText();
    }

    /// <summary>
    /// Filter predicate for properties based on name and description
    /// </summary>
    private bool PropertyFilterPredicate(WmiProperty property, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return property.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
               property.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
               property.Type.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Updates the help text based on current selection state
    /// </summary>
    private void UpdateHelpText()
    {
        if (SelectedClass == null)
        {
            HelpText = "Select a class to view properties";
        }
        else if (SelectedClass.WmiClass?.Properties?.Count == 0)
        {
            HelpText = "No properties available for the selected class";
        }
        else if (SelectedProperty == null)
        {
            HelpText = "Select a property to view detailed information";
        }
        else
        {
            var lazyText = SelectedProperty.IsLazy ? " (Lazy property - loaded on demand)" : "";
            var keyText = SelectedProperty.IsKey ? " [Key Property]" : "";
            HelpText = $"Property: {SelectedProperty.Name} ({SelectedProperty.Type}){keyText}{lazyText}";
        }
    }
}