using System.Windows;
using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for array properties providing text-based editing with validation and tips.
/// </summary>
public static class ArrayEditor
{
    /// <summary>
    /// Creates an array editor with input validation and tips
    /// </summary>
    public static StackPanel Create(PropertyHierarchyItem propertyItem, Type arrayType)
    {
        var elementType = arrayType.GetElementType();
        if (elementType == null)
        {
            return new StackPanel
            {
                Children = { new TextBlock { Text = "Invalid array type" } }
            };
        }

        var textBox = UIHelpers.CreateStandardTextBox(
            FormatArrayValueForEditing(propertyItem.Value),
            $"Enter {EditorInfrastructure.GetFriendlyTypeName(elementType)} values separated by commas or semicolons",
            propertyItem,
            null,
            (text, originalValue) => CustomArrayValidation(text, originalValue, elementType));

        // Enable multiline editing with Shift+Enter for new lines
        textBox.AcceptsReturn = true;
        textBox.TextWrapping = System.Windows.TextWrapping.Wrap;

        // Optionally, set a reasonable height for multiline editing
        textBox.MinHeight = 24;
        textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        textBox.ToolTip = $"Enter {EditorInfrastructure.GetFriendlyTypeName(elementType)} values separated by commas or semicolons";

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        stackPanel.Children.Add(textBox);

        var tipText = $"Enter {EditorInfrastructure.GetFriendlyTypeName(elementType)} values separated by commas or semicolons";
        var tipTextBlock = UIHelpers.CreateTipTextBlock(tipText);

        stackPanel.Children.Add(tipTextBlock);
        return stackPanel;
    }

    /// <summary>
    /// Formats an array value for text editing
    /// </summary>
    public static string FormatArrayValueForEditing(object? value)
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
    /// Parses array value from text input
    /// </summary>
    public static Array ParseArrayValueFromText(string text, Type elementType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.CreateInstance(elementType, 0);
        }

        var values = SplitArrayInput(text);
        var array = Array.CreateInstance(elementType, values.Count);
        var converter = System.ComponentModel.TypeDescriptor.GetConverter(elementType);

        for (int i = 0; i < values.Count; i++)
        {
            try
            {
                var convertedValue = ConvertElement(values[i], elementType, converter);
                array.SetValue(convertedValue, i);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot convert '{values[i]}' to {elementType.Name}: {ex.Message}", ex);
            }
        }
        return array;
    }

    /// <summary>
    /// Converts a string value to the specified element type using the provided converter.
    /// </summary>
    private static object? ConvertElement(string value, Type elementType, System.ComponentModel.TypeConverter converter)
    {
        if (elementType == typeof(string))
        {
            return value;
        }
        else if (converter != null && converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromString(value);
        }
        else
        {
            return System.Convert.ChangeType(value, elementType);
        }
    }

    private static ValidationManager.ValidationResult CustomArrayValidation(string text, object? originalValue, Type elementType)
    {
        try
        {
            var parsedArray = ParseArrayValueFromText(text, elementType);
            bool isModified = !ValidationManager.AreArraysEqual(parsedArray, originalValue as Array);
            return ValidationManager.ValidationResult.Valid(parsedArray, isModified);
        }
        catch (Exception ex)
        {
            return ValidationManager.ValidationResult.Error($"Array parsing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Splits the input string into array elements, respecting quoted delimiters.
    /// </summary>
    private static List<string> SplitArrayInput(string text)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue; // Don't include the quote itself
            }
            if ((c == ',' || c == ';') && !inQuotes)
            {
                var val = current.ToString().Trim();
                if (!string.IsNullOrEmpty(val))
                    values.Add(val);
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        var lastVal = current.ToString().Trim();
        if (!string.IsNullOrEmpty(lastVal))
            values.Add(lastVal);
        return values;
    }
}