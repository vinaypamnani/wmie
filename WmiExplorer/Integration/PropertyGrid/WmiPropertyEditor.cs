using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WmiExplorer.Common.Logging;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.PropertyGrid;
using WmiExplorer.PropertyGrid.Abstractions;
using WmiExplorer.PropertyGrid.Editors.Core;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.PropertyGrid;

/// <summary>
/// Specialized property editor for WMI properties that provides UI creation and interaction logic.
/// This separates WMI-specific UI concerns from the generic PropertyGrid library.
/// </summary>
public class WmiPropertyEditor : IPropertyEditor, IDisposable
{
    private readonly IMessengerService _messengerService;

    // Cache for ViewModels to avoid creating them multiple times for the same property
    private readonly ConcurrentDictionary<string, WmiPropertyViewModel> _viewModels = new();

    private readonly IWmiService _wmiService;

    /// <summary>
    /// Initializes a new instance of the WmiPropertyEditor with required dependencies.
    /// </summary>
    /// <param name="wmiService">The WMI service for WMI operations</param>
    /// <param name="messengerService">The messenger service for messaging</param>
    public WmiPropertyEditor(IWmiService wmiService, IMessengerService messengerService)
    {
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
    }

    /// <summary>
    /// Determines whether this editor can handle the specified property item.
    /// This editor handles WMI object, reference, and DateTime properties.
    /// </summary>
    /// <param name="propertyItem">The property item to check</param>
    /// <returns>True if this is a WMI object, reference, or DateTime property</returns>
    public bool CanHandle(PropertyHierarchyItem propertyItem)
    {
        if (propertyItem?.PropertyDescriptor is WmiPropertyDescriptor wmiDescriptor)
        {
            return wmiDescriptor.IsObject || wmiDescriptor.IsReference ||
                   wmiDescriptor.PropertyData.Type == System.Management.CimType.DateTime;
        }
        return false;
    }

    /// <summary>
    /// Creates an editor UI element for the specified WMI property item.
    /// </summary>
    /// <param name="propertyItem">The property item to create an editor for</param>
    /// <returns>A UI element that can edit the WMI property</returns>
    public UIElement CreateEditor(PropertyHierarchyItem propertyItem)
    {
        if (propertyItem?.PropertyDescriptor is not WmiPropertyDescriptor wmiDescriptor)
        {
            throw new ArgumentException("PropertyItem must have a WmiPropertyDescriptor", nameof(propertyItem));
        }

        if (wmiDescriptor.IsObject)
        {
            return CreateObjectEditor(propertyItem, wmiDescriptor);
        }
        else if (wmiDescriptor.IsReference)
        {
            return CreateReferenceEditor(propertyItem, wmiDescriptor);
        }
        else if (wmiDescriptor.PropertyData.Type == System.Management.CimType.DateTime)
        {
            return CreateDateTimeEditor(propertyItem, wmiDescriptor);
        }

        throw new ArgumentException("Property is not a WMI object, reference, or DateTime type", nameof(propertyItem));
    }

    private void ApplyValidation(Control control, PropertyHierarchyItem propertyItem)
    {
        var current = propertyItem.Value;
        var original = propertyItem.OriginalValue;
        bool isModified;
        if (current is System.Management.ManagementBaseObject mboCurrent && original is System.Management.ManagementBaseObject mboOriginal)
        {
            isModified = !AreWmiObjectsEqual(mboCurrent, mboOriginal);
        }
        else
        {
            isModified = !ValidationManager.AreValuesEqual(current, original);
        }
        if (isModified)
        {
            ValidationManager.SetValidationModified(control);
        }
        else
        {
            ValidationManager.SetValidationNormal(control);
        }
    }

    private static bool AreWmiObjectsEqual(System.Management.ManagementBaseObject? obj1, System.Management.ManagementBaseObject? obj2)
    {
        if (obj1 == null && obj2 == null) { return true; }
        if (obj1 == null || obj2 == null) { return false; }
        if (obj1.Properties.Count != obj2.Properties.Count) { return false; }
        foreach (System.Management.PropertyData prop in obj1.Properties)
        {
            var otherProp = obj2.Properties[prop.Name];
            if (otherProp == null) { return false; }
            if (!ValidationManager.AreValuesEqual(prop.Value, otherProp.Value))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Compares two WMI values for equality, handling nulls and strings appropriately
    /// </summary>
    private static bool AreWmiValuesEqual(object? value1, object? value2)
    {
        // Handle null cases
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;

        // For WMI values, use string comparison (most WMI values are strings)
        return value1.ToString() == value2.ToString();
    }

    /// <summary>
    /// Determines if cancelling reference value loading is possible.
    /// </summary>
    private bool CanCancelLoadReferenceValues(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.CancelLoadReferenceValuesCommand?.CanExecute(null) ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cancels the loading of reference values.
    /// </summary>
    private void CancelLoadReferenceValues(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            viewModel.CancelLoadReferenceValuesCommand?.Execute(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cancelling reference values load for property '{PropertyName}'", wmiDescriptor.Name);
        }
    }

    /// <summary>
    /// Determines if editing object is possible for this property.
    /// </summary>
    private bool CanEditObject(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.EditObjectCommand?.CanExecute(null) ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines if loading reference values is possible for this property.
    /// </summary>
    private bool CanLoadReferenceValues(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.LoadReferenceValuesCommand?.CanExecute(null) ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates an editor for WMI DateTime-type properties with a string input.
    /// </summary>
    private UIElement CreateDateTimeEditor(PropertyHierarchyItem propertyItem, WmiPropertyDescriptor wmiDescriptor)
    {
        // Create a TextBox with custom WMI DateTime validation
        var textBox = PropertyEditorUtils.CreateStandardTextBox(
            wmiDescriptor.PropertyData.Value?.ToString(),
            "Enter WMI DateTime (e.g., 20231201120000.000000+060)",
            propertyItem,
            null, // margin
            ValidateWmiDateTime
        );

        textBox.IsReadOnly = wmiDescriptor.IsReadOnly;

        return textBox;
    }

    /// <summary>
    /// Creates an editor for WMI object-type properties with an Edit button.
    /// </summary>
    private UIElement CreateObjectEditor(PropertyHierarchyItem propertyItem, WmiPropertyDescriptor wmiDescriptor)
    {
        // Read-only TextBox showing object info using utility method
        var textBox = PropertyEditorUtils.CreateStandardTextBox(
            GetObjectDisplayText(wmiDescriptor),
            null,
            propertyItem
        );

        PropertyEditorUtils.InitializeEditor(textBox, propertyItem);
        textBox.IsReadOnly = true;
        textBox.TextWrapping = TextWrapping.Wrap;

        // Validation/modified tracking (for object reference, if value can change)
        textBox.TextChanged += (s, e) => ApplyValidation(textBox, propertyItem);
        textBox.Loaded += (s, e) => ApplyValidation(textBox, propertyItem);

        // Edit Button
        var editButton = new Button
        {
            Content = "Edit...",
            Width = 54,
            IsEnabled = CanEditObject(wmiDescriptor)
        };

        // Handle edit button click
        editButton.Click += (s, e) =>
        {
            try
            {
                var result = EditObject(wmiDescriptor);
                // Set the edited object as the new value for the property grid
                if (result != null)
                {
                    propertyItem.Value = result;
                    textBox.Text = GetObjectDisplayText(wmiDescriptor);
                    ApplyValidation(textBox, propertyItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error editing object: {ex.Message}", "Edit Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        return PropertyEditorUtils.CreateGridWithActionButton(textBox, editButton, editButton.Width);
    }

    /// <summary>
    /// Creates an editor for WMI reference-type properties with ComboBox and Load/Cancel buttons.
    /// </summary>
    private UIElement CreateReferenceEditor(PropertyHierarchyItem propertyItem, WmiPropertyDescriptor wmiDescriptor)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // ComboBox for reference values
        var comboBox = new ComboBox
        {
            IsEditable = true,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = PropertyEditorUtils.CONTROL_MARGIN_STANDARD,
            ItemsSource = GetReferenceValues(wmiDescriptor),
            Text = GetReferenceText(wmiDescriptor)
        };

        PropertyEditorUtils.InitializeEditor(comboBox, propertyItem);

        // Bind SelectedItem to propertyItem.Value for validation/modified tracking
        comboBox.SetBinding(ComboBox.SelectedItemProperty, new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });

        // Apply MaxWidth constraint using the same logic as base PropertyEditor
        PropertyEditorUtils.ApplyMaxWidthConstraint(comboBox, grid, 120); // Account for Load/Cancel buttons

        // update value on TextChanged if possible
        if (comboBox.IsEditable)
        {
            comboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) =>
            {
                var tb = (s as ComboBox)?.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
                if (tb != null && comboBox.Text != GetReferenceText(wmiDescriptor))
                {
                    SetReferenceText(wmiDescriptor, comboBox.Text);
                }
                ApplyValidation(comboBox, propertyItem);
            }));
        }

        // Handle selection changes
        comboBox.SelectionChanged += (s, e) =>
        {
            if (comboBox.SelectedItem is string selectedText && selectedText != GetReferenceText(wmiDescriptor))
            {
                SetReferenceText(wmiDescriptor, selectedText);
                comboBox.Text = selectedText;
            }
            ApplyValidation(comboBox, propertyItem);
        };

        // Focus handling for selection
        PropertyEditorUtils.AttachSelectOnFocus(comboBox, propertyItem);

        Grid.SetColumn(comboBox, 0);
        grid.Children.Add(comboBox);

        // Load Button
        var loadButton = new Button
        {
            Content = "Load",
            Width = 60,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = CanLoadReferenceValues(wmiDescriptor)
        };

        // Handle load button click
        loadButton.Click += async (s, e) =>
        {
            try
            {
                loadButton.IsEnabled = false;
                loadButton.Content = "Loading...";

                // Track the current selected value (prefer SelectedItem, fallback to Text)
                var previousSelected = comboBox.SelectedItem as string;
                if (string.IsNullOrEmpty(previousSelected))
                    previousSelected = comboBox.Text;

                await LoadReferenceValuesAsync(wmiDescriptor);

                // Update the ComboBox items
                var newItems = GetReferenceValues(wmiDescriptor);
                comboBox.ItemsSource = newItems;

                // Try to re-select the previous value if it exists, otherwise select the first item
                if (!string.IsNullOrEmpty(previousSelected) && newItems.Contains(previousSelected))
                {
                    comboBox.SelectedItem = previousSelected;
                }
                else if (newItems.Count > 0)
                {
                    comboBox.SelectedItem = newItems[0];
                }

                loadButton.IsEnabled = CanLoadReferenceValues(wmiDescriptor);
                loadButton.Content = "Load";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading reference values: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                loadButton.IsEnabled = true;
                loadButton.Content = "Load";
            }
        };

        Grid.SetColumn(loadButton, 1);
        grid.Children.Add(loadButton);

        // Cancel Button
        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 60,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = CanCancelLoadReferenceValues(wmiDescriptor)
        };

        // Handle cancel button click
        cancelButton.Click += (s, e) =>
        {
            try
            {
                CancelLoadReferenceValues(wmiDescriptor);
                loadButton.IsEnabled = CanLoadReferenceValues(wmiDescriptor);
                cancelButton.IsEnabled = CanCancelLoadReferenceValues(wmiDescriptor);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cancelling load: {ex.Message}", "Cancel Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        Grid.SetColumn(cancelButton, 2);
        grid.Children.Add(cancelButton);

        return grid;
    }

    /// <summary>
    /// Creates a new ViewModel for the specified WMI property descriptor.
    /// </summary>
    private WmiPropertyViewModel CreateViewModel(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            // Get the ManagementScope for the ViewModel
            var scope = wmiDescriptor.GetManagementScope();

            // Create the ViewModel with PropertyData using the injected WmiService and MessengerService
            return new WmiPropertyViewModel(wmiDescriptor.PropertyData, _wmiService, scope, false, _messengerService);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating ViewModel for property '{PropertyName}'", wmiDescriptor.Name);
            throw;
        }
    }

    /// <summary>
    /// Edits an object property using the PropertyEditorDialog.
    /// </summary>
    private object? EditObject(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = CreateViewModel(wmiDescriptor); // Create a new ViewModel for editing so we don't modify the cached one
            viewModel.EditObjectCommand?.Execute(null);
            return viewModel.Value; // Return the edited object;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error editing object: {ex.Message}", "Edit Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    /// <summary>
    /// Gets the display text for object properties from the ViewModel.
    /// </summary>
    private string GetObjectDisplayText(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.ObjectDisplayText ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets or creates a ViewModel for the specified WMI property descriptor.
    /// </summary>
    private WmiPropertyViewModel GetOrCreateViewModel(WmiPropertyDescriptor wmiDescriptor)
    {
        // Create a unique key for this property
        var key = $"{wmiDescriptor.Name}_{wmiDescriptor.GetHashCode()}";

        return _viewModels.GetOrAdd(key, _ => CreateViewModel(wmiDescriptor));
    }

    /// <summary>
    /// Gets the current reference text value for display and editing.
    /// </summary>
    private string GetReferenceText(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.ReferenceText ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the collection of reference values for reference properties.
    /// </summary>
    private ObservableCollection<string> GetReferenceValues(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.ReferenceValues ?? new ObservableCollection<string>();
        }
        catch
        {
            return new ObservableCollection<string>();
        }
    }

    /// <summary>
    /// Validates if a string is in valid WMI DateTime format
    /// </summary>
    private static bool IsValidWmiDateTime(string dateTimeString)
    {
        if (string.IsNullOrEmpty(dateTimeString))
            return false;

        try
        {
            // Try to convert using WMI's ManagementDateTimeConverter
            var dateTime = System.Management.ManagementDateTimeConverter.ToDateTime(dateTimeString);
            return true;
        }
        catch
        {
            // Also try standard DateTime parsing as fallback
            return DateTime.TryParse(dateTimeString, out _);
        }
    }

    /// <summary>
    /// Loads reference values for reference-type properties.
    /// </summary>
    private async Task LoadReferenceValuesAsync(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            if (viewModel.LoadReferenceValuesCommand?.CanExecute(null) == true)
            {
                await viewModel.LoadReferenceValuesCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading reference values for property '{PropertyName}'", wmiDescriptor.Name);
            throw;
        }
    }

    /// <summary>
    /// Sets the current reference text value.
    /// </summary>
    private void SetReferenceText(WmiPropertyDescriptor wmiDescriptor, string value)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            viewModel.ReferenceText = value;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting reference text for property '{PropertyName}' to value '{Value}'", wmiDescriptor.Name, value);
        }
    }

    /// <summary>
    /// Validates if a string is in valid WMI DateTime format
    /// </summary>
    private static ValidationManager.ValidationResult ValidateWmiDateTime(string text, object? originalValue)
    {
        if (string.IsNullOrEmpty(text))
            return ValidationManager.ValidationResult.Valid(null, !AreWmiValuesEqual(originalValue, null));
        if (IsValidWmiDateTime(text))
            return ValidationManager.ValidationResult.Valid(text, !AreWmiValuesEqual(originalValue, text));
        return ValidationManager.ValidationResult.Error("Invalid WMI DateTime format. Expected format: YYYYMMDDHHMMSS.mmmmmm±UUU (e.g., 20250708120000.000000-000)");
    }

    #region IDisposable
    private bool _disposed = false;

    /// <summary>
    /// Disposes of managed ViewModels and clears the cache.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var viewModel in _viewModels.Values)
            {
                if (viewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _viewModels.Clear();
            _disposed = true;
        }
    }

    #endregion
}