using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WmiExplorer.Common.Logging;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.Views.Dialogs;
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
    private readonly WmiPropertyValueConverter _propertyValueConverter = new WmiPropertyValueConverter();

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

        // Handle WMI object arrays as embedded object arrays
        if (wmiDescriptor.IsObject && wmiDescriptor.PropertyData.IsArray)
        {
            return CreateEmbeddedObjectArrayEditor(propertyItem, wmiDescriptor);
        }

        // Handle WMI object arrays as not supported
        if (wmiDescriptor.IsObject && wmiDescriptor.PropertyData.IsArray)
        {
            var textBox = PropertyEditorUtils.CreateStandardTextBox("<Not supported>", null, propertyItem);
            textBox.IsReadOnly = true;
            textBox.TextWrapping = TextWrapping.Wrap;
            return textBox;
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
            if (otherProp == null)
            {
                return false;
            }
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
    /// Creates an editor for embedded object arrays (ManagementBaseObject[]).
    /// </summary>
    private UIElement CreateEmbeddedObjectArrayEditor(PropertyHierarchyItem propertyItem, WmiPropertyDescriptor wmiDescriptor)
    {
        var viewModel = CreateViewModel(wmiDescriptor);

        var panel = new StackPanel();
        var array = propertyItem.Value as System.Management.ManagementBaseObject[];
        var items = new ObservableCollection<System.Management.ManagementBaseObject>(array ?? Array.Empty<System.Management.ManagementBaseObject>());

        // Hidden proxy ComboBox for validation state (ComBoBox is used because textbox gets the "Ctrl+Z" message)
        var validationProxy = new ComboBox
        {
            Visibility = Visibility.Collapsed,
            Width = 0,
            IsReadOnly = true,
            Focusable = false,
            IsTabStop = false,
            IsHitTestVisible = false
        };
        panel.Children.Add(validationProxy);
        // Ensure ValidationState.Normal is set on load so CardPropertyEditor can find it
        ValidationManager.SetValidationNormal(validationProxy);

        // ListView for embedded objects
        var listView = new ListView
        {
            Margin = new Thickness(0, 0, 0, 0),
            MinHeight = 40,
            MaxHeight = 200,
            Style = (Style)Application.Current.FindResource("ModernListViewStyle"),
            Background = System.Windows.Media.Brushes.Transparent,
            SelectionMode = SelectionMode.Single,
            SelectedIndex = -1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        // Ensure ListView items stretch horizontally
        listView.SetValue(ItemsControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch);
        // Override ControlTemplate to fully disable hover background
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentPresenterFactory.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
        contentPresenterFactory.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
        contentPresenterFactory.SetValue(ContentPresenter.ContentTemplateSelectorProperty, new TemplateBindingExtension(ContentControl.ContentTemplateSelectorProperty));
        borderFactory.AppendChild(contentPresenterFactory);
        var template = new ControlTemplate(typeof(ListViewItem)) { VisualTree = borderFactory };
        listView.ItemContainerStyle = new Style(typeof(ListViewItem))
        {
            Setters = {
                new Setter(Control.TemplateProperty, template)
            }
        };

        // Helper to create a ListView item UI for an embedded object
        UIElement CreateEmbeddedObjectListItem(System.Management.ManagementBaseObject mbo)
        {
            var displayText = _propertyValueConverter.ConvertToString(mbo, typeof(System.Management.ManagementBaseObject));
            var textBox = PropertyEditorUtils.CreateStandardTextBox(displayText, null, null, new Thickness(4, 0, 0, 0));
            textBox.IsReadOnly = true;
            textBox.TextWrapping = TextWrapping.Wrap;

            ValidationManager.SetValidationNormal(textBox);

            var editButton = new Button
            {
                Content = "Edit",
                Width = 54
            };
            editButton.Click += (s, e) =>
            {
                var edited = EditObject(mbo);
                if (edited is System.Management.ManagementBaseObject editedMbo)
                {
                    // Set validation state to Modified unconditionally
                    ValidationManager.SetValidationModified(validationProxy);
                    var idx = items.IndexOf(mbo);
                    if (idx >= 0)
                    {
                        items[idx] = editedMbo;
                        propertyItem.Value = items.ToArray();
                        textBox.Text = _propertyValueConverter.ConvertToString(editedMbo, typeof(System.Management.ManagementBaseObject));
                        listView.Items[idx] = CreateEmbeddedObjectListItem(editedMbo);
                    }
                }
            };

            var removeButton = new Button
            {
                Content = "Remove",
                Width = 60,
                Margin = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            removeButton.Click += (s, e) =>
            {
                items.Remove(mbo);
                // propertyItem.Value and validation will be updated by the CollectionChanged handler
            };

            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            actionPanel.Children.Add(editButton);
            actionPanel.Children.Add(removeButton);

            return PropertyEditorUtils.CreateGridWithActionPanel(textBox, actionPanel, editButton.Width + removeButton.Width + 8 + 4);
        }

        // Populate ListView initially
        listView.Items.Clear();
        foreach (var mbo in items)
        {
            listView.Items.Add(CreateEmbeddedObjectListItem(mbo));
        }

        // Rebuild ListView only when items are added/removed
        items.CollectionChanged += (s, e) =>
        {
            listView.Items.Clear();
            foreach (var mbo in items)
            {
                listView.Items.Add(CreateEmbeddedObjectListItem(mbo));
            }
            propertyItem.Value = items.ToArray();
            // Set validation state to Modified on any add/remove
            ValidationManager.SetValidationModified(validationProxy);
        };

        // Expander to contain the ListView
        var expander = new Expander
        {
            Header = $"Embedded Objects ({items.Count})",
            IsExpanded = true,
            Content = listView
        };

        // Update expander header on collection change
        items.CollectionChanged += (s, e) =>
        {
            expander.Header = $"Embedded Objects ({items.Count})";
        };

        // Add button as before
        var addButton = new Button {
            Content = "Add...",
            Width = 60
        };
        addButton.HorizontalAlignment = HorizontalAlignment.Left;
        addButton.Click += (s, e) =>
        {
            var className = viewModel.TargetClassName;
            var scope = wmiDescriptor.GetManagementScope();
            if (!string.IsNullOrEmpty(className) && scope != null)
            {
                try
                {
                    var newObj = WmiObjectFactory.CreateTemplateObject(className, scope);
                    if (newObj != null)
                    {
                        var owner = Application.Current.MainWindow;
                        var edited = Presentation.Views.Dialogs.PropertyEditorDialog.ShowEditor(owner, newObj, _messengerService, $"Add {className}");
                        if (edited != null)
                        {
                            items.Add(edited);
                            propertyItem.Value = items.ToArray();
                            // Set validation state to Modified on add
                            ValidationManager.SetValidationModified(validationProxy);
                        }
                    }
                    else
                    {
                        Log.Error("Error creating embedded object: {ClassName}. New object is null. ", className);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error creating embedded object: {ClassName}", className);
                }
            }
            else
            {
                Log.Error("Error creating embedded object: {PropertyName}. Target class name or scope is null. ", wmiDescriptor.Name);
            }
        };
        panel.Children.Add(expander);
        panel.Children.Add(addButton);
        // No need to call validation on load or selection change
        listView.SelectionChanged += (s, e) =>
        {
            listView.SelectedIndex = -1;
        };
        return panel;
    }

    /// <summary>
    /// Creates an editor for WMI object-type properties with an Edit button.
    /// </summary>
    private UIElement CreateObjectEditor(PropertyHierarchyItem propertyItem, WmiPropertyDescriptor wmiDescriptor)
    {
        var viewModel = CreateViewModel(wmiDescriptor); //Create a new ViewModel for editing so we don't modify the cached one
        var mbo = viewModel.Value as System.Management.ManagementBaseObject;

        // Read-only TextBox showing object info using value converter
        var displayText = _propertyValueConverter.ConvertToString(mbo, typeof(System.Management.ManagementBaseObject));
        var textBox = PropertyEditorUtils.CreateStandardTextBox(
            displayText,
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
            IsEnabled = CanEditObject(wmiDescriptor)
        };

        void UpdateEditButton()
        {
            var mbo = propertyItem.Value as System.Management.ManagementBaseObject;
            if (mbo != null)
            {
                editButton.Content = "Edit";
                editButton.Width = 54;
            }
            else
            {
                editButton.Content = "Create";
                editButton.Width = 64;
            }
        }

        textBox.TextChanged += (s, e) => UpdateEditButton();
        UpdateEditButton();

        // Handle edit button click
        editButton.Click += (s, e) =>
        {
            try
            {

                var className = viewModel.TargetClassName;
                var scope = wmiDescriptor.GetManagementScope();
                var objectToEdit = propertyItem.Value as System.Management.ManagementBaseObject;
                if (!string.IsNullOrEmpty(className) && scope != null)
                {
                    if (objectToEdit == null)
                    {
                        objectToEdit = WmiObjectFactory.CreateTemplateObject(className, scope);
                    }

                    var result = EditObject(objectToEdit);

                    // Set the edited object as the new value for the property grid
                    if (result != null)
                    {
                        propertyItem.Value = result;
                        textBox.Text = _propertyValueConverter.ConvertToString(result, typeof(System.Management.ManagementBaseObject));
                        ApplyValidation(textBox, propertyItem);
                    }
                    else
                    {
                        Log.Error("Error editing object: {PropertyName}. New object is null. ", wmiDescriptor.Name);
                    }
                }
                else
                {
                    Log.Error("Error editing object: {PropertyName}. Target class name or scope is null. ", wmiDescriptor.Name);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error editing object: {PropertyName}", wmiDescriptor.Name);
                MessageBoxDialog.Show($"Error editing object: {ex.Message}", "Edit Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, System.Windows.Application.Current.MainWindow);
            }
        };

        // Remove Button
        var removeButton = new Button
        {
            Content = "Remove",
            Width = 60,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        removeButton.Click += (s, e) =>
        {
            propertyItem.Value = null;
            textBox.Text = "<null>";
            UpdateEditButton();
            ApplyValidation(textBox, propertyItem);
        };

        // StackPanel for action buttons
        var actionPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        actionPanel.Children.Add(editButton);
        actionPanel.Children.Add(removeButton);

        return PropertyEditorUtils.CreateGridWithActionPanel(textBox, actionPanel, editButton.Width + removeButton.Width + 8);
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
                MessageBoxDialog.Show($"Error loading reference values: {ex.Message}", "Load Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Warning, System.Windows.Application.Current.MainWindow);
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
                MessageBoxDialog.Show($"Error cancelling load: {ex.Message}", "Cancel Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Warning, System.Windows.Application.Current.MainWindow);
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
    private object? EditObject(System.Management.ManagementBaseObject? mbo, string? title = null)
    {
        try
        {
            if (mbo == null) return null;

            var owner = Application.Current.MainWindow;
            var displayName = _propertyValueConverter.ConvertToString(mbo, typeof(System.Management.ManagementBaseObject));
            var dialogTitle = title ?? $"Edit object: {displayName}";
            var edited = Presentation.Views.Dialogs.PropertyEditorDialog.ShowEditor(owner, mbo, _messengerService, dialogTitle);
            return edited;
        }
        catch (Exception ex)
        {
            MessageBoxDialog.Show($"Error editing object: {ex.Message}", "Edit Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, System.Windows.Application.Current.MainWindow);
            return null;
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