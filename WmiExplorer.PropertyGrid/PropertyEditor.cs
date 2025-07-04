using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Provides a content control that displays an appropriate editor for a property based on its type.
/// </summary>
public class PropertyEditor : ContentControl
{
    public static readonly DependencyProperty PropertyItemProperty =
        DependencyProperty.Register(nameof(PropertyItem), typeof(PropertyHierarchyItem), typeof(PropertyEditor),
            new PropertyMetadata(null, OnPropertyItemChanged));

    /// <summary>
    /// Gets or sets whether to display the editor with card-like styling
    /// </summary>
    public static readonly DependencyProperty UseCardStyleProperty =
        DependencyProperty.Register(nameof(UseCardStyle), typeof(bool), typeof(PropertyEditor),
            new PropertyMetadata(false, OnUseCardStyleChanged));

    // Margin constants for consistent spacing throughout the PropertyEditor
    private static readonly Thickness CARD_MARGIN = new Thickness(-12, 2, 2, 2);

    // Compact card margins
    private static readonly Thickness CARD_PADDING = new Thickness(6, 4, 6, 4);

    // Compact space between name and type text
    private static readonly Thickness CHECKBOX_HEX_MARGIN = new Thickness(6, 0, 3, 0);

    // Compact card padding
    private static readonly Thickness CONTROL_MARGIN_CARD = new Thickness(0, 1, 0, 1);

    // Minimal margins for inline controls
    private static readonly Thickness CONTROL_MARGIN_STANDARD = new Thickness(4, 2, 4, 2);

    // Control margins in standard mode
    private static readonly Thickness HEADER_BOTTOM_MARGIN = new Thickness(0, 0, 0, 4);

    // Reduced space for traditional layout
    private static readonly Thickness NAME_TEXT_MARGIN = new Thickness(0, 0, 6, 0);

    // Compact hex checkbox margin
    private static readonly Thickness TIP_TEXT_MARGIN = new Thickness(3, 3, 3, 3);

    // Compact array tip text margin

    static PropertyEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyEditor),
            new FrameworkPropertyMetadata(typeof(PropertyEditor)));
    }

    /// <summary>
    /// Gets or sets the property item to edit.
    /// </summary>
    public PropertyHierarchyItem PropertyItem
    {
        get => (PropertyHierarchyItem)GetValue(PropertyItemProperty);
        set => SetValue(PropertyItemProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to display the editor with card-like styling
    /// </summary>
    public bool UseCardStyle
    {
        get => (bool)GetValue(UseCardStyleProperty);
        set => SetValue(UseCardStyleProperty, value);
    }

    // Helper to attach focus event for selection
    private void AttachSelectOnFocus(Control control, PropertyHierarchyItem? propertyItem)
    {
        if (propertyItem != null && control != null)
        {
            control.GotFocus += (s, e) => propertyItem.IsSelected = true;
        }
    }

    /// <summary>
    /// Constrains TextBox widths to prevent card overflow by binding to available space
    /// </summary>
    private void ConstrainTextBoxWidths(FrameworkElement element, Grid parentGrid)
    {
        if (element is TextBox textBox)
        {
            // Bind MaxWidth to the parent grid's ActualWidth minus padding/margins
            var binding = new Binding("ActualWidth")
            {
                Source = parentGrid,
                Converter = new MaxWidthConverter()
            };
            textBox.SetBinding(TextBox.MaxWidthProperty, binding);
        }
        else if (element is Panel panel)
        {
            // Recursively apply to child elements
            foreach (UIElement child in panel.Children)
            {
                if (child is FrameworkElement childElement)
                {
                    ConstrainTextBoxWidths(childElement, parentGrid);
                }
            }
        }
        else if (element is Decorator decorator && decorator.Child is FrameworkElement decoratorChild)
        {
            // Handle Border, ScrollViewer, etc.
            ConstrainTextBoxWidths(decoratorChild, parentGrid);
        }
        else if (element is ContentControl contentControl && contentControl.Content is FrameworkElement contentElement)
        {
            // Handle other content controls
            ConstrainTextBoxWidths(contentElement, parentGrid);
        }
    }

    /// <summary>
    /// Creates an editor for array properties supporting comma/semicolon-separated values
    /// </summary>
    private void CreateArrayEditor(PropertyHierarchyItem propertyItem, Type arrayType)
    {
        var elementType = arrayType.GetElementType();
        if (elementType == null) return;

        // Create TextBox using helper method for consistency
        var textBox = CreateStandardTextBox(
            FormatArrayValueForEditing(propertyItem.Value),
            $"Enter {GetFriendlyTypeName(elementType)} values separated by commas or semicolons",
            propertyItem // Pass propertyItem to set IsSelected on focus
        );

        textBox.ToolTip = $"Enter {GetFriendlyTypeName(elementType)} values separated by commas or semicolons";

        textBox.LostFocus += (sender, e) =>
        {
            if (sender is TextBox tb)
            {
                try
                {
                    var arrayValue = ParseArrayValueFromText(tb.Text, elementType);
                    propertyItem.Value = arrayValue;
                }
                catch (Exception ex)
                {
                    // Reset to original value if parsing fails
                    tb.Text = FormatArrayValueForEditing(propertyItem.Value);
                    System.Diagnostics.Debug.WriteLine($"Array parsing error: {ex.Message}");
                }
            }
        };

        // Create StackPanel to hold TextBox and tip
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Add TextBox to StackPanel
        stackPanel.Children.Add(textBox);

        // Create helpful tip TextBlock
        var tipTextBlock = new TextBlock
        {
            Text = $"💡 Enter {GetFriendlyTypeName(elementType)} values separated by commas or semicolons",
            FontSize = 11,
            FontStyle = FontStyles.Italic,
            Opacity = 0.7,
            Margin = TIP_TEXT_MARGIN, // Use consistent margin constant
            TextWrapping = TextWrapping.Wrap
        };

        // Try to set foreground color from resources
        try
        {
            tipTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "PropertyGridForegroundBrush");
        }
        catch
        {
            // Fallback to gray if resource not available
            tipTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
        }

        // Add tip to StackPanel
        stackPanel.Children.Add(tipTextBlock);

        Content = CreateStandardEditorContainer(stackPanel);

        // Wrap content in card-style border if enabled
        if (UseCardStyle && Content is UIElement element)
        {
            Content = WrapContentIfNeeded(element, propertyItem);
        }
    }

    /// <summary>
    /// Creates an inline layout where the editor appears on the same line as the property name
    /// </summary>
    private UIElement CreateInlineCardLayout(UIElement content, PropertyHierarchyItem propertyItem)
    {
        var mainGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Two columns: property info and editor
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Property name and type in the first column
        var nameStackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameText = new TextBlock
        {
            Text = propertyItem.DisplayName ?? propertyItem.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11, // Slightly smaller for compact layout
            Margin = NAME_TEXT_MARGIN,
            VerticalAlignment = VerticalAlignment.Center
        };

        var typeText = new TextBlock
        {
            Text = $"[{GetFriendlyTypeName(propertyItem.PropertyType)}]",
            FontStyle = FontStyles.Italic,
            FontSize = 10, // Smaller type information
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center
        };

        nameStackPanel.Children.Add(nameText);
        nameStackPanel.Children.Add(typeText);

        Grid.SetColumn(nameStackPanel, 0);
        mainGrid.Children.Add(nameStackPanel);

        // Editor content in the second column
        if (content is FrameworkElement fe)
        {
            fe.Margin = new Thickness(8, 0, 0, 0); // Small left margin to separate from text
            fe.VerticalAlignment = VerticalAlignment.Center;
            fe.HorizontalAlignment = HorizontalAlignment.Right;
        }

        Grid.SetColumn(content, 1);
        mainGrid.Children.Add(content);

        return mainGrid;
    }

    /// <summary>
    /// Creates an integer editor with hex/decimal display option
    /// </summary>
    private Grid CreateIntegerEditor(PropertyHierarchyItem propertyItem)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // TextBox takes available space
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // CheckBox takes only what it needs

        // Create TextBox using helper method
        var textBox = CreateStandardTextBox(null, "Enter number (decimal or 0xHEX)", propertyItem);

        // Create CheckBox
        var checkBox = new CheckBox
        {
            Content = "Hex",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = CHECKBOX_HEX_MARGIN, // Use consistent margin constant
            FontSize = 11 // Slightly smaller font to reduce width
        };

        // Set grid positions
        Grid.SetColumn(textBox, 0);
        Grid.SetColumn(checkBox, 1);

        // Add to grid
        grid.Children.Add(textBox);
        grid.Children.Add(checkBox);

        // Initialize hex display based on value
        bool isHexadecimal = ShouldDefaultToHex(propertyItem.Value);
        checkBox.IsChecked = isHexadecimal;

        // Set up data binding and event handling
        SetupIntegerEditorBinding(textBox, checkBox, propertyItem);

        return grid;
    }

    /// <summary>
    /// Creates an optimized layout for the card based on the property type
    /// </summary>
    private UIElement CreateOptimizedCardLayout(UIElement content, PropertyHierarchyItem propertyItem)
    {
        var propertyType = propertyItem.PropertyType;
        var isSimpleType = IsSimpleType(propertyType);

        if (isSimpleType && !(content is StackPanel)) // Don't inline complex content like arrays
        {
            // For simple types (bool, enum, small text), use inline layout
            return CreateInlineCardLayout(content, propertyItem);
        }
        else
        {
            // For complex types (arrays, long text), use traditional two-row layout
            return CreateTraditionalCardLayout(content, propertyItem);
        }
    }

    /// <summary>
    /// Creates a standard container for editor controls to ensure consistent alignment
    /// </summary>
    private FrameworkElement CreateStandardEditorContainer(UIElement editor)
    {
        // For card mode, we want all editors to have consistent structure
        if (UseCardStyle)
        {
            // In card mode, wrap in a simple container with consistent properties
            var container = new Border
            {
                Child = editor,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            return container;
        }
        else
        {
            // In traditional mode, return as-is for backward compatibility
            return editor as FrameworkElement ?? new Border { Child = editor };
        }
    }

    /// <summary>
    /// Creates a standardized TextBox for property editing
    /// </summary>
    private TextBox CreateStandardTextBox(string? initialText = null, string? placeholder = null, PropertyHierarchyItem? propertyItem = null)
    {
        var textBox = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = UseCardStyle ? CONTROL_MARGIN_CARD : CONTROL_MARGIN_STANDARD,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxWidth = double.PositiveInfinity
        };
        if (!string.IsNullOrEmpty(initialText))
        {
            textBox.Text = initialText;
        }
        textBox.Tag = placeholder ?? "Enter value";
        try
        {
            textBox.Style = (Style)FindResource("PropertyGridTextBoxWithPlaceholder");
        }
        catch { }
        if (propertyItem != null)
        {
            textBox.GotFocus += (s, e) => propertyItem.IsSelected = true;
        }
        return textBox;
    }

    /// <summary>
    /// Creates a traditional two-row layout for complex editors
    /// </summary>
    private UIElement CreateTraditionalCardLayout(UIElement content, PropertyHierarchyItem propertyItem)
    {
        var cardGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Property name header
        var nameStackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = HEADER_BOTTOM_MARGIN
        };

        var nameText = new TextBlock
        {
            Text = propertyItem.DisplayName ?? propertyItem.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11, // Compact size
            Margin = NAME_TEXT_MARGIN
        };

        var typeText = new TextBlock
        {
            Text = $"[{GetFriendlyTypeName(propertyItem.PropertyType)}]",
            FontStyle = FontStyles.Italic,
            FontSize = 10, // Compact size
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center
        };

        nameStackPanel.Children.Add(nameText);
        nameStackPanel.Children.Add(typeText);

        Grid.SetRow(nameStackPanel, 0);
        cardGrid.Children.Add(nameStackPanel);

        // Property editor content
        if (content is FrameworkElement fe)
        {
            fe.Margin = new Thickness(0);
            fe.HorizontalAlignment = HorizontalAlignment.Stretch;
            ConstrainTextBoxWidths(fe, cardGrid);
        }
        else
        {
            var container = new Border
            {
                Margin = new Thickness(0),
                Child = content,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            content = container;
        }

        Grid.SetRow(content, 1);
        cardGrid.Children.Add(content);

        return cardGrid;
    }

    /// <summary>
    /// Formats an array value for text editing
    /// </summary>
    private string FormatArrayValueForEditing(object? value)
    {
        if (value is Array array)
        {
            var values = new List<string>();
            for (int i = 0; i < array.Length; i++)
            {
                var item = array.GetValue(i);
                values.Add(item?.ToString() ?? "");
            }
            return string.Join(", ", values);
        }
        return string.Empty;
    }

    /// <summary>
    /// Gets a friendly type name for display purposes
    /// </summary>
    private string GetFriendlyTypeName(Type type)
    {
        return type.Name switch
        {
            "String" => "String",
            "Int32" => "Int32 [integer]",
            "Int64" => "Int64 [long integer]",
            "UInt32" => "UInt32 [unsigned integer]",
            "UInt64" => "UInt64 [unsigned long]",
            "Int16" => "Int16 [short integer]",
            "UInt16" => "UInt16 [unsigned short]",
            "Byte" => "Byte (0-255)",
            "SByte" => "SByte [signed byte] (-128 to 127)",
            "Double" => "Double [decimal number]",
            "Single" => "Single [decimal number]",
            "Decimal" => "Decimal [decimal number]",
            "Boolean" => "Boolean [true/false]",
            "DateTime" => "DateTime [date and time]",
            _ => type.Name.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Determines if a property type is simple enough for inline layout
    /// </summary>
    private bool IsSimpleType(Type? propertyType)
    {
        if (propertyType == null) return false;

        return propertyType == typeof(bool) || propertyType == typeof(bool?) ||
               propertyType.IsEnum ||
               propertyType == typeof(byte) || propertyType == typeof(byte?) ||
               propertyType == typeof(sbyte) || propertyType == typeof(sbyte?) ||
               propertyType == typeof(short) || propertyType == typeof(short?) ||
               propertyType == typeof(ushort) || propertyType == typeof(ushort?) ||
               propertyType == typeof(DateTime) || propertyType == typeof(DateTime?);
    }

    private static void OnPropertyItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PropertyEditor editor && e.NewValue is PropertyHierarchyItem propertyItem)
        {
            editor.UpdateEditor(propertyItem);
        }
    }

    private static void OnUseCardStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PropertyEditor editor && editor.PropertyItem != null)
        {
            editor.UpdateEditor(editor.PropertyItem);
        }
    }

    /// <summary>
    /// Parses comma/semicolon-separated text into an array of the specified element type
    /// </summary>
    private Array ParseArrayValueFromText(string text, Type elementType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.CreateInstance(elementType, 0);
        }

        // Split by comma or semicolon, remove empty entries
        var separators = new[] { ',', ';' };
        var stringValues = text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                              .Select(s => s.Trim())
                              .Where(s => !string.IsNullOrEmpty(s))
                              .ToArray();

        var array = Array.CreateInstance(elementType, stringValues.Length);
        var converter = System.ComponentModel.TypeDescriptor.GetConverter(elementType);

        for (int i = 0; i < stringValues.Length; i++)
        {
            try
            {
                object? convertedValue = null;

                if (elementType == typeof(string))
                {
                    convertedValue = stringValues[i];
                }
                else if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    convertedValue = converter.ConvertFromString(stringValues[i]);
                }
                else
                {
                    // Try direct conversion for common types
                    convertedValue = Convert.ChangeType(stringValues[i], elementType);
                }

                array.SetValue(convertedValue, i);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot convert '{stringValues[i]}' to {elementType.Name}: {ex.Message}", ex);
            }
        }

        return array;
    }

    /// <summary>
    /// Sets up binding and event handling for integer editor
    /// </summary>
    private void SetupIntegerEditorBinding(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem)
    {
        // Update display format when checkbox changes
        checkBox.Checked += (s, e) => UpdateIntegerDisplay(textBox, propertyItem, true);
        checkBox.Unchecked += (s, e) => UpdateIntegerDisplay(textBox, propertyItem, false);

        // Handle text changes
        textBox.LostFocus += (s, e) => UpdatePropertyFromIntegerText(textBox, checkBox, propertyItem);

        // Initialize display
        UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
    }

    /// <summary>
    /// Determines if a value should default to hexadecimal display
    /// </summary>
    private static bool ShouldDefaultToHex(object? value)
    {
        if (value == null) return false;

        try
        {
            var longValue = Convert.ToInt64(value);
            // Default to hex for values larger than 0x80000000 (2147483648)
            return longValue > 0x80000000;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateEditor(PropertyHierarchyItem? propertyItem)
    {
        // Exit if property is read-only or null
        if (propertyItem == null || propertyItem.IsReadOnly)
        {
            // For read-only properties, use a TextBlock
            var textBlock = new TextBlock
            {
                Text = propertyItem?.FormattedValue ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
                // Use consistent margin constants
                Margin = UseCardStyle ? CONTROL_MARGIN_CARD : CONTROL_MARGIN_STANDARD
            };
            Content = textBlock;

            // Wrap content in card-style border if enabled
            if (UseCardStyle && propertyItem != null && Content is UIElement readOnlyElement)
            {
                Content = WrapContentIfNeeded(readOnlyElement, propertyItem);
            }
            return;
        }

        // Handle different property types
        var propertyType = propertyItem.PropertyType;

        // Check for array types first
        if (propertyType != null && propertyType.IsArray)
        {
            CreateArrayEditor(propertyItem, propertyType);
            return;
        }
        if (propertyType == typeof(bool) || propertyType == typeof(bool?))
        {
            // Use CheckBox for boolean properties
            var checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                // Minimal margin for inline display in card mode
                Margin = UseCardStyle ? new Thickness(0) : CONTROL_MARGIN_STANDARD
            };

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);
            AttachSelectOnFocus(checkBox, propertyItem);
            Content = CreateStandardEditorContainer(checkBox);
        }
        else if (propertyType != null && propertyType.IsEnum)
        {
            // Use ComboBox for enum properties
            var comboBox = new ComboBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                // Minimal margin for inline display in card mode
                Margin = UseCardStyle ? new Thickness(0) : CONTROL_MARGIN_STANDARD,
                ItemsSource = Enum.GetValues(propertyType),
                MinWidth = 100 // Ensure minimum usable width
            };

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            comboBox.SetBinding(ComboBox.SelectedItemProperty, binding);
            AttachSelectOnFocus(comboBox, propertyItem);
            Content = CreateStandardEditorContainer(comboBox);
        }
        else if (propertyType == typeof(int) || propertyType == typeof(int?) ||
                propertyType == typeof(long) || propertyType == typeof(long?) ||
                propertyType == typeof(uint) || propertyType == typeof(uint?) ||
                propertyType == typeof(ulong) || propertyType == typeof(ulong?) ||
                propertyType == typeof(short) || propertyType == typeof(short?) ||
                propertyType == typeof(ushort) || propertyType == typeof(ushort?) ||
                propertyType == typeof(byte) || propertyType == typeof(byte?) ||
                propertyType == typeof(sbyte) || propertyType == typeof(sbyte?))
        {
            // Create hex/decimal editor for integer types
            Content = CreateStandardEditorContainer(CreateIntegerEditor(propertyItem));
        }
        else if (propertyType == typeof(double) || propertyType == typeof(float) || propertyType == typeof(decimal))
        {
            // Use a TextBox with numeric validation for non-integer numeric types
            var placeholderText = propertyType == typeof(decimal) ? "Enter decimal number (e.g., 123.45)" : "Enter decimal number";
            var textBox = CreateStandardTextBox(null, placeholderText, propertyItem);

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            };

            textBox.SetBinding(TextBox.TextProperty, binding);
            Content = CreateStandardEditorContainer(textBox);
        }
        else if (propertyType == typeof(string))
        {
            // Use TextBox for string properties
            var textBox = CreateStandardTextBox(null, "Enter text", propertyItem);

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            };

            textBox.SetBinding(TextBox.TextProperty, binding);
            Content = CreateStandardEditorContainer(textBox);
        }
        else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
        {
            // Use DatePicker for DateTime properties
            var datePicker = new DatePicker
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                // Minimal margin for inline display in card mode
                Margin = UseCardStyle ? new Thickness(0) : CONTROL_MARGIN_STANDARD,
                MinWidth = 180 // Ensure minimum usable width
            };

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            datePicker.SetBinding(DatePicker.SelectedDateProperty, binding);
            AttachSelectOnFocus(datePicker, propertyItem);
            Content = CreateStandardEditorContainer(datePicker);
        }
        else
        {
            // Default to TextBox for other types
            var textBox = CreateStandardTextBox(propertyItem.FormattedValue, $"Enter {GetFriendlyTypeName(propertyItem.PropertyType)} value", propertyItem);

            textBox.TextChanged += (sender, e) =>
            {
                if (sender is TextBox tb && propertyItem != null)
                {
                    try
                    {
                        // Try to convert the text to the property type
                        var converter = System.ComponentModel.TypeDescriptor.GetConverter(propertyItem.PropertyType);
                        if (converter != null && converter.CanConvertFrom(typeof(string)))
                        {
                            propertyItem.Value = converter.ConvertFromString(tb.Text);
                        }
                    }
                    catch
                    {
                        // If conversion fails, don't update the value
                    }
                }
            };

            Content = CreateStandardEditorContainer(textBox);
        }

        // Wrap content in card-style border if enabled
        if (UseCardStyle && Content is UIElement element)
        {
            Content = WrapContentIfNeeded(element, propertyItem);
        }
    }

    /// <summary>
    /// Updates the integer display format
    /// </summary>
    private void UpdateIntegerDisplay(TextBox textBox, PropertyHierarchyItem propertyItem, bool isHexadecimal)
    {
        if (propertyItem.Value == null)
        {
            textBox.Text = string.Empty;
            return;
        }

        try
        {
            var longValue = Convert.ToInt64(propertyItem.Value);
            textBox.Text = isHexadecimal ? $"0x{longValue:X}" : longValue.ToString();
        }
        catch
        {
            textBox.Text = propertyItem.Value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Updates the property value from integer text input
    /// </summary>
    private void UpdatePropertyFromIntegerText(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem)
    {
        var text = textBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            propertyItem.Value = null;
            return;
        }

        try
        {
            long longValue;

            // Parse hex or decimal
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var hexValue = text.Substring(2);
                longValue = Convert.ToInt64(hexValue, 16);
            }
            else
            {
                longValue = Convert.ToInt64(text);
            }

            // Convert to target type
            var targetType = propertyItem.PropertyType;
            if (targetType == typeof(int) || targetType == typeof(int?))
                propertyItem.Value = (int)longValue;
            else if (targetType == typeof(uint) || targetType == typeof(uint?))
                propertyItem.Value = (uint)longValue;
            else if (targetType == typeof(long) || targetType == typeof(long?))
                propertyItem.Value = longValue;
            else if (targetType == typeof(ulong) || targetType == typeof(ulong?))
                propertyItem.Value = (ulong)longValue;
            else if (targetType == typeof(short) || targetType == typeof(short?))
                propertyItem.Value = (short)longValue;
            else if (targetType == typeof(ushort) || targetType == typeof(ushort?))
                propertyItem.Value = (ushort)longValue;
            else if (targetType == typeof(byte) || targetType == typeof(byte?))
                propertyItem.Value = (byte)longValue;
            else if (targetType == typeof(sbyte) || targetType == typeof(sbyte?))
                propertyItem.Value = (sbyte)longValue;
            else
                propertyItem.Value = longValue;

            // Update display to show the correctly formatted value
            UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
        }
        catch (Exception)
        {
            // If parsing fails, revert to original display
            UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
        }
    }

    /// <summary>
    /// Wraps content in a card-style border if UseCardStyle is enabled
    /// </summary>
    private UIElement WrapContentIfNeeded(UIElement content, PropertyHierarchyItem propertyItem)
    {
        if (!UseCardStyle)
            return content;

        // Clear the content from this control first to avoid logical parent conflicts
        Content = null;

        // Create card-style border similar to MethodExecutionDialog
        var cardBorder = new Border
        {
            // Use consistent margin and padding constants
            Margin = CARD_MARGIN, // Aligns cards with category headers
            Padding = CARD_PADDING, // Standard card inner padding
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch // Stretch to full available width
        };

        // Try to set dynamic resources (these may not work in design-time)
        try
        {
            cardBorder.SetResourceReference(Border.BackgroundProperty, "SecondaryBackgroundBrush");
            cardBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        }
        catch
        {
            // Fallback colors if dynamic resources aren't available
            cardBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 128, 128, 128)); // Light gray with transparency
            cardBorder.BorderBrush = System.Windows.Media.Brushes.LightGray;
        }

        // Set up selection highlighting with left accent border
        // Bind BorderThickness to IsSelected using a converter
        var thicknessBinding = new System.Windows.Data.Binding("IsSelected")
        {
            Source = propertyItem,
            Converter = new SelectionBorderThicknessConverter()
        };
        cardBorder.SetBinding(Border.BorderThicknessProperty, thicknessBinding);

        // Bind BorderBrush to IsSelected using a converter
        var brushBinding = new System.Windows.Data.Binding("IsSelected")
        {
            Source = propertyItem,
            Converter = new SelectionBorderBrushConverter()
        };
        cardBorder.SetBinding(Border.BorderBrushProperty, brushBinding);

        // Create layout based on property type for optimal compactness
        var cardContent = CreateOptimizedCardLayout(content, propertyItem);
        cardBorder.Child = cardContent;
        return cardBorder;
    }

    /// <summary>
    /// Converter to calculate MaxWidth for TextBoxes based on available space
    /// </summary>
    private class MaxWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double actualWidth && actualWidth > 0)
            {
                // Leave some padding for borders, margins, and potential scrollbars
                return Math.Max(100, actualWidth - 2); // Minimum 100px, subtract 2px for padding - this was 40 before but was causing issues with extra padding
            }
            return 300.0; // Fallback width
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for border brush based on selection state
    /// </summary>
    private class SelectionBorderBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                // Use PropertyGridAccentBrush when selected
                try
                {
                    return Application.Current.FindResource("PropertyGridAccentBrush");
                }
                catch
                {
                    // Fallback to blue accent if PropertyGridAccentBrush not found
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
                }
            }

            // Use default border brush when not selected
            try
            {
                return Application.Current.FindResource("BorderBrush");
            }
            catch
            {
                // Fallback to light gray
                return System.Windows.Media.Brushes.LightGray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for border thickness based on selection state
    /// </summary>
    private class SelectionBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                // 4px left border when selected, 1px on other sides
                return new Thickness(4, 1, 1, 1);
            }
            // 1px all around when not selected
            return new Thickness(1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}