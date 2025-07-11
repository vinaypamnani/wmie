using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WmiExplorer.PropertyGrid.Editors.Core;

/// <summary>
/// Manages validation logic for property editors including error states,
/// success feedback, and value conversion validation.
/// </summary>
public static class ValidationManager
{
    // Attached property to store the validation state
    public static readonly DependencyProperty ValidationStateProperty =
        DependencyProperty.RegisterAttached("ValidationState", typeof(ValidationState), typeof(ValidationManager),
            new PropertyMetadata(ValidationState.Normal, OnValidationStateChanged));

    // Attached property to track if validation is in progress to prevent recursive calls
    private static readonly DependencyProperty IsValidatingProperty =
        DependencyProperty.RegisterAttached("IsValidating", typeof(bool), typeof(ValidationManager), new PropertyMetadata(false));

    // Attached property to store the original value for comparison
    private static readonly DependencyProperty OriginalValueProperty =
        DependencyProperty.RegisterAttached("OriginalValue", typeof(object), typeof(ValidationManager), new PropertyMetadata(null));

    /// <summary>
    /// Adds validation behavior to a TextBox for property editing
    /// </summary>
    public static void AddValidationBehavior(TextBox textBox, PropertyHierarchyItem propertyItem, Func<string, object, ValidationResult>? customValidation = null)
    {
        // Store original values for reset functionality
        var originalBorderBrush = textBox.BorderBrush;
        var originalToolTip = textBox.ToolTip;

        // Initialize original value tracking
        InitializeOriginalValue(textBox, propertyItem);

        // Add TextChanged validation (visual feedback only, no property updates)
        textBox.TextChanged += (sender, e) =>
        {
            if (sender is TextBox tb && !GetIsValidating(tb))
            {
                if (customValidation != null)
                {
                    var originalValue = GetOriginalValue(tb);
                    var result = customValidation(tb.Text, originalValue);
                    if (!result.IsValid)
                    {
                        SetValidationError(tb, result.ErrorMessage ?? "Invalid value");
                    }
                    else if (result.IsModified)
                    {
                        SetValidationModified(tb, "Value modified");
                    }
                    else
                    {
                        SetValidationNormal(tb);
                    }
                }
                else if (ShouldValidateOnTextChanged(propertyItem))
                {
                    // Use default validation for types that use TypeConverter (visual feedback only)
                    ValidateTextBoxValue(tb, propertyItem, originalBorderBrush, originalToolTip);
                }
            }
        };

        // Add LostFocus handler to actually update the property value
        textBox.LostFocus += (sender, e) =>
        {
            if (sender is TextBox tb && !GetIsValidating(tb))
            {
                if (customValidation != null)
                {
                    var originalValue = GetOriginalValue(tb);
                    var result = customValidation(tb.Text, originalValue);
                    if (result.IsValid)
                    {
                        propertyItem.Value = result.ParsedValue;
                        // Do not update original value here!
                    }
                }
                else
                {
                    UpdatePropertyValueFromText(tb, propertyItem, originalBorderBrush, originalToolTip);
                }
            }
        };

        // Add key handler to reset on Escape
        textBox.KeyDown += (sender, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape && sender is TextBox tb)
            {
                // Reset to original value and clear error state
                Converters.CaretPositionHelper.SetTextPreservingCaret(tb, propertyItem.FormattedValue ?? string.Empty);
                ClearValidationError(tb, originalBorderBrush, originalToolTip);
                e.Handled = true;
            }
        };
    }

    /// <summary>
    /// Applies appropriate validation styling based on whether the value has been modified
    /// This should be called by all editors when updating display formats to preserve validation state
    /// </summary>
    public static void ApplyValidationStyling(TextBox textBox, PropertyHierarchyItem propertyItem)
    {
        if (IsValueModified(textBox, propertyItem))
        {
            SetValidationState(textBox, ValidationState.Modified);
        }
        else
        {
            SetValidationState(textBox, ValidationState.Normal);
        }
    }

    /// <summary>
    /// Compares two arrays for equality
    /// </summary>
    public static bool AreArraysEqual(Array? array1, Array? array2)
    {
        // Handle null cases
        if (array1 == null && array2 == null) return true;
        if (array1 == null || array2 == null) return false;

        // Check if lengths are different
        if (array1.Length != array2.Length) return false;

        // Compare each element
        for (int i = 0; i < array1.Length; i++)
        {
            var item1 = array1.GetValue(i);
            var item2 = array2.GetValue(i);

            if (!AreValuesEqual(item1, item2))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares two values for equality, handling nulls appropriately
    /// </summary>
    public static bool AreValuesEqual(object? value1, object? value2)
    {
        // Handle null cases
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;

        // For numeric types, ensure we're comparing the same type
        if (EditorInfrastructure.IsNumericType(value1.GetType()) && EditorInfrastructure.IsNumericType(value2.GetType()))
        {
            try
            {
                // Convert both to decimal for comparison to handle different numeric types
                var decimal1 = Convert.ToDecimal(value1);
                var decimal2 = Convert.ToDecimal(value2);
                return decimal1 == decimal2;
            }
            catch
            {
                // Fall back to standard comparison if conversion fails
                return value1.Equals(value2);
            }
        }

        // For other types, use standard equality comparison
        return value1.Equals(value2);
    }

    /// <summary>
    /// Clears validation error state from a TextBox
    /// </summary>
    public static void ClearValidationError(TextBox textBox)
    {
        // Restore default styling
        textBox.ClearValue(Control.BorderBrushProperty);
        textBox.ClearValue(Control.BorderThicknessProperty);
        textBox.ClearValue(Control.ToolTipProperty);
        textBox.ClearValue(Control.BackgroundProperty);
    }

    /// <summary>
    /// Clears validation error state from a TextBox with original values
    /// </summary>
    public static void ClearValidationError(TextBox textBox, Brush originalBorderBrush, object originalToolTip)
    {
        // Restore original border
        textBox.BorderBrush = originalBorderBrush;
        textBox.BorderThickness = new Thickness(1);

        // Restore original tooltip
        textBox.ToolTip = originalToolTip;

        // Clear error background
        textBox.ClearValue(Control.BackgroundProperty);
    }

    public static bool GetIsValidating(DependencyObject obj) => (bool)obj.GetValue(IsValidatingProperty);

    public static ValidationState GetValidationState(DependencyObject obj) => (ValidationState)obj.GetValue(ValidationStateProperty);

    /// <summary>
    /// Initializes the original value tracking for a TextBox
    /// Call this when creating an editor to establish the baseline value
    /// </summary>
    public static void InitializeOriginalValue(TextBox textBox, PropertyHierarchyItem propertyItem)
    {
        SetOriginalValue(textBox, propertyItem.Value);
        // Set up validation binding and initialize to Normal state
        SetupValidationBinding(textBox);
        SetValidationState(textBox, ValidationState.Normal);
    }

    /// <summary>
    /// Checks if the current property value differs from the original value
    /// </summary>
    public static bool IsValueModified(TextBox textBox, PropertyHierarchyItem propertyItem)
    {
        var originalValue = GetOriginalValue(textBox);
        return !AreValuesEqual(propertyItem.Value, originalValue);
    }

    public static void SetIsValidating(DependencyObject obj, bool value) => obj.SetValue(IsValidatingProperty, value);

    /// <summary>
    /// Sets the validation state to error for a TextBox
    /// </summary>
    public static void SetValidationError(TextBox textBox, string errorMessage)
    {
        SetValidationState(textBox, ValidationState.Error);
        textBox.ToolTip = CreateErrorTooltip(errorMessage);
    }

    /// <summary>
    /// Sets the validation state to modified for a TextBox
    /// </summary>
    public static void SetValidationModified(TextBox textBox, string successMessage = "Value modified")
    {
        SetValidationState(textBox, ValidationState.Modified);
        textBox.ToolTip = CreateSuccessTooltip(successMessage);
    }

    /// <summary>
    /// Sets the validation state to normal for a TextBox
    /// </summary>
    public static void SetValidationNormal(TextBox textBox)
    {
        SetValidationState(textBox, ValidationState.Normal);
        textBox.ClearValue(Control.ToolTipProperty);
    }

    public static void SetValidationState(DependencyObject obj, ValidationState value)
    {
        // Ensure binding is set up if this is the first time setting state
        if (obj is TextBox textBox && GetValidationState(obj) == ValidationState.Normal && value != ValidationState.Normal)
        {
            SetupValidationBinding(textBox);
        }
        obj.SetValue(ValidationStateProperty, value);
    }

    /// <summary>
    /// Shows validation error state on a TextBox
    /// </summary>
    public static void ShowValidationError(TextBox textBox, string errorMessage)
    {
        // Set error border
        textBox.BorderBrush = Brushes.Red;
        textBox.BorderThickness = new Thickness(2);

        // Set error tooltip
        textBox.ToolTip = CreateErrorTooltip(errorMessage);

        // Optional: Add background tint to make error more visible
        textBox.Background = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0));
    }

    /// <summary>
    /// Shows validation success state on a TextBox for modified values
    /// </summary>
    public static void ShowValidationSuccess(TextBox textBox, string successMessage = "Value modified")
    {
        // Set success border
        textBox.BorderBrush = Brushes.Green;
        textBox.BorderThickness = new Thickness(2);

        // Set success tooltip
        textBox.ToolTip = CreateSuccessTooltip(successMessage);

        // Add background tint to make success more visible
        textBox.Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0));
    }

    private static object CreateErrorTooltip(string errorMessage)
    {
        return CreateIconTooltip(
            iconGlyph: "\uEA39", // StatusErrorFull
            iconColor: Brushes.Red,
            mainMessage: $"Validation Error: {errorMessage}"
        );
    }

    private static object CreateIconTooltip(string iconGlyph, Brush iconColor, string mainMessage)
    {
        var icon = new TextBlock
        {
            Text = iconGlyph,
            Foreground = iconColor,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        try
        {
            var style = (Style)Application.Current.FindResource("PropertyGridIconStyle");
            icon.Style = style;
        }
        catch { /* Style not found, fallback to default */ }

        var mainText = new TextBlock
        {
            Text = mainMessage,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        var secondaryText = new TextBlock
        {
            Text = "Press Escape to reset to original value.",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };

        var row1 = new StackPanel { Orientation = Orientation.Horizontal };
        row1.Children.Add(icon);
        row1.Children.Add(mainText);

        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(row1);
        panel.Children.Add(secondaryText);
        return panel;
    }

    private static object CreateSuccessTooltip(string successMessage)
    {
        return CreateIconTooltip(
            iconGlyph: "\uE946", // Success icon
            iconColor: Brushes.Green,
            mainMessage: successMessage
        );
    }

    private static object GetOriginalValue(DependencyObject obj) => obj.GetValue(OriginalValueProperty);

    /// <summary>
    /// Callback when validation state changes - sets up the background binding (only once)
    /// </summary>
    private static void OnValidationStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox && e.OldValue == null)
        {
            // Only set up binding on first access, not on every state change
            SetupValidationBinding(textBox);
        }
    }

    private static void SetOriginalValue(DependencyObject obj, object? value) => obj.SetValue(OriginalValueProperty, value);

    /// <summary>
    /// Sets up the validation state binding for a TextBox (should only be called once per TextBox)
    /// </summary>
    private static void SetupValidationBinding(TextBox textBox)
    {
        // Set up binding to convert ValidationState to background brush
        var backgroundBinding = new Binding
        {
            Path = new PropertyPath(ValidationStateProperty),
            Source = textBox,
            Converter = new ValidationStateToBackgroundConverter(),
            Mode = BindingMode.OneWay
        };

        textBox.SetBinding(Control.BackgroundProperty, backgroundBinding);

        // Set up border brush binding
        var borderBrushBinding = new Binding
        {
            Path = new PropertyPath(ValidationStateProperty),
            Source = textBox,
            Converter = new ValidationStateToBorderConverter(),
            Mode = BindingMode.OneWay
        };

        textBox.SetBinding(Control.BorderBrushProperty, borderBrushBinding);

        // Set up border thickness binding
        var borderThicknessBinding = new Binding
        {
            Path = new PropertyPath(ValidationStateProperty),
            Source = textBox,
            Converter = new ValidationStateToBorderThicknessConverter(),
            Mode = BindingMode.OneWay
        };

        textBox.SetBinding(Control.BorderThicknessProperty, borderThicknessBinding);
    }

    /// <summary>
    /// Determines if a property should validate on TextChanged (vs binding validation)
    /// </summary>
    private static bool ShouldValidateOnTextChanged(PropertyHierarchyItem propertyItem)
    {
        // Apply validation styling to ALL TextBox editors for consistent behavior
        // This includes string, numeric, DateTime, and other text-based property types
        var type = propertyItem.PropertyType;

        // Only skip validation for types that don't use TextBox editors
        return !type.IsEnum && !type.IsArray && type != typeof(bool) && type != typeof(bool?);
    }

    /// <summary>
    /// Updates the property value from TextBox text on focus loss (property conversion only, no styling)
    /// </summary>
    private static void UpdatePropertyValueFromText(TextBox textBox, PropertyHierarchyItem propertyItem, Brush originalBorderBrush, object originalToolTip)
    {
        try
        {
            SetIsValidating(textBox, true);

            var converter = System.ComponentModel.TypeDescriptor.GetConverter(propertyItem.PropertyType);

            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                try
                {
                    // Convert and update the property value
                    var convertedValue = converter.ConvertFromString(textBox.Text);

                    try
                    {
                        propertyItem.Value = convertedValue;
                        // Property conversion successful - no styling changes here
                        // Validation styling is handled by TextChanged events only
                    }
                    catch (Exception setValueEx)
                    {
                        // If setting the value failed, show error but don't change styling here
                        // The TextChanged validation will handle the styling
                        System.Diagnostics.Debug.WriteLine($"Property assignment failed: {setValueEx.Message}");
                    }
                }
                catch (Exception conversionEx)
                {
                    // Conversion failed - but styling is handled by TextChanged validation
                    System.Diagnostics.Debug.WriteLine($"Value conversion failed: {conversionEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't change styling - TextChanged validation handles that
            System.Diagnostics.Debug.WriteLine($"Property update failed: {ex.Message}");
        }
        finally
        {
            SetIsValidating(textBox, false);
        }
    }

    /// <summary>
    /// Validates the TextBox value using TypeConverter without updating text (to prevent caret jumping)
    /// </summary>
    private static void ValidateTextBoxValue(TextBox textBox, PropertyHierarchyItem propertyItem, Brush originalBorderBrush, object originalToolTip)
    {
        // Prevent recursive validation calls
        if (GetIsValidating(textBox)) return;

        try
        {
            SetIsValidating(textBox, true);

            var converter = System.ComponentModel.TypeDescriptor.GetConverter(propertyItem.PropertyType);

            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                try
                {
                    // Test conversion without updating the property value during typing
                    // This prevents triggering additional TextChanged events that cause caret jumping
                    var convertedValue = converter.ConvertFromString(textBox.Text);

                    // Only show visual feedback - don't update the property value during typing
                    // The actual property update happens on LostFocus
                    if (convertedValue != null)
                    {
                        // Value is valid - check if it differs from the ORIGINAL value stored when editor was created
                        var storedOriginalValue = GetOriginalValue(textBox);
                        bool valueModified = !AreValuesEqual(storedOriginalValue, convertedValue);

                        if (valueModified)
                        {
                            SetValidationModified(textBox, "Modified value (will be rounded or truncated if needed, depending on property type)");
                        }
                        else
                        {
                            // Value is valid and matches original - normal state
                            SetValidationNormal(textBox);
                        }
                    }
                    else
                    {
                        // Check if null differs from original value
                        var storedOriginalValue = GetOriginalValue(textBox);
                        bool valueModified = !AreValuesEqual(storedOriginalValue, null);

                        if (valueModified)
                        {
                            SetValidationModified(textBox, "Modified value (will be rounded or truncated if needed, depending on property type)");
                        }
                        else
                        {
                            SetValidationNormal(textBox);
                        }
                    }
                }
                catch (Exception conversionEx)
                {
                    // Show error styling but don't change the text
                    SetValidationError(textBox, conversionEx.Message);
                }
            }
            else
            {
                SetValidationError(textBox, "Cannot convert this value to the required type.");
            }
        }
        catch (Exception ex)
        {
            // Show visual feedback for validation error without changing text
            SetValidationError(textBox, ex.Message);
        }
        finally
        {
            SetIsValidating(textBox, false);
        }
    }

    public struct ValidationResult
    {
        private ValidationResult(bool isValid, bool isModified, object? parsedValue, string? errorMessage)
        {
            IsValid = isValid;
            IsModified = isModified;
            ParsedValue = parsedValue;
            ErrorMessage = errorMessage;
        }

        public string? ErrorMessage { get; }
        public bool IsModified { get; }
        public bool IsValid { get; }
        public object? ParsedValue { get; }

        public static ValidationResult Error(string errorMessage) => new ValidationResult(false, false, null, errorMessage);

        public static ValidationResult Valid(object? parsedValue, bool isModified) => new ValidationResult(true, isModified, parsedValue, null);
    }
}

/// <summary>
/// Converts ValidationState to background brush
/// </summary>
public class ValidationStateToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationState state)
        {
            return state switch
            {
                ValidationState.Normal => GetPropertyGridBackgroundBrush(),
                ValidationState.Modified => new SolidColorBrush(Color.FromArgb(30, 0, 255, 0)),
                ValidationState.Error => new SolidColorBrush(Color.FromArgb(30, 255, 0, 0)),
                _ => GetPropertyGridBackgroundBrush()
            };
        }
        return GetPropertyGridBackgroundBrush();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static Brush GetPropertyGridBackgroundBrush()
    {
        try
        {
            // Try to get the PropertyGridBackgroundBrush resource
            if (Application.Current?.TryFindResource("PropertyGridBackgroundBrush") is Brush brush)
            {
                return brush;
            }
        }
        catch
        {
            // Fall back if resource not found
        }

        // Fallback to system background brush
        return SystemColors.WindowBrush;
    }
}

/// <summary>
/// Converts ValidationState to border brush
/// </summary>
public class ValidationStateToBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationState state)
        {
            return state switch
            {
                ValidationState.Normal => SystemColors.ControlDarkBrush,
                ValidationState.Modified => Brushes.Green,
                ValidationState.Error => Brushes.Red,
                _ => SystemColors.ControlDarkBrush
            };
        }
        return SystemColors.ControlDarkBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ValidationState to border thickness
/// </summary>
public class ValidationStateToBorderThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationState state)
        {
            return state switch
            {
                ValidationState.Normal => new Thickness(1),
                ValidationState.Modified => new Thickness(2),
                ValidationState.Error => new Thickness(2),
                _ => new Thickness(1)
            };
        }
        return new Thickness(1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Represents the validation state of a property editor
/// </summary>
public enum ValidationState
{
    /// <summary>
    /// Normal state - no validation styling needed
    /// </summary>
    Normal,

    /// <summary>
    /// Modified state - value has been changed from original
    /// </summary>
    Modified,

    /// <summary>
    /// Error state - validation failed
    /// </summary>
    Error
}