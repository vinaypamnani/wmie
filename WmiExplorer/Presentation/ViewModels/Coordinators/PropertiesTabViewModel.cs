using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Models;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Properties tab. Manages property-related functionality
/// and UI operations for the properties list view.
/// </summary>
public partial class PropertiesTabViewModel : SelectionAwareViewModelBase
{
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _helpText = "Select a class to view properties";

    private FilterHelper<WmiProperty>? _propertyFilterHelper;

    [ObservableProperty]
    private string _propertyFilterText = string.Empty;

    [ObservableProperty]
    private WmiProperty? _selectedProperty;

    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    public PropertiesTabViewModel(
                 IMessengerService messengerService,
                 ISettingsService settingsService,
                 SelectionManager selectionManager) : base(messengerService, selectionManager)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

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
    /// Called when the selected class changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedClassChanged(WmiClassViewModel? selectedClass)
    {
        // Update filtered properties when class selection changes
        UpdateFilteredProperties(selectedClass);
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
    /// Update the filtered properties based on the provided class selection
    /// </summary>
    private void UpdateFilteredProperties(WmiClassViewModel? selectedClass)
    {
        // Reset selected property when class changes
        SelectedProperty = null;

        // Dispose of the old filter helper
        _propertyFilterHelper?.Dispose();
        _propertyFilterHelper = null;

        // Create new filter helper if we have properties
        if (selectedClass?.Properties != null)
        {

            _propertyFilterHelper = new FilterHelper<WmiProperty>(
                selectedClass.Properties,
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
    }

    /// <summary>
    /// Updates the help text based on current selection state
    /// </summary>
    private void UpdateHelpText()
    {
        var selectedClass = SelectionManager.GetSelectedClass();
        if (selectedClass == null)
        {
            HelpText = "Select a class to view properties";
        }
        else if (selectedClass?.WmiClass?.Properties?.Count == 0)
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