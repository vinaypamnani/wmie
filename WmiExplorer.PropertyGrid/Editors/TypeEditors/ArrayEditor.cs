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
            propertyItem
        );

        textBox.ToolTip = $"Enter {EditorInfrastructure.GetFriendlyTypeName(elementType)} values separated by commas or semicolons";

        textBox.LostFocus += (sender, e) =>
        {
            if (sender is TextBox tb)
            {
                try
                {
                    // Store the original value for comparison
                    var originalValue = propertyItem.Value;

                    var arrayValue = ParseArrayValueFromText(tb.Text, elementType);
                    propertyItem.Value = arrayValue;

                    // Check if the array was actually changed
                    bool arrayChanged = !ValidationManager.AreArraysEqual(originalValue as Array, arrayValue);

                    if (arrayChanged)
                    {
                        // Show success state for modified arrays
                        ValidationManager.SetValidationModified(tb);
                    }
                    else
                    {
                        // Clear any previous styling if array unchanged
                        ValidationManager.SetValidationNormal(tb);
                    }
                }
                catch (Exception ex)
                {
                    ValidationManager.SetValidationError(tb, $"Array parsing error: {ex.Message}");
                    // Leave the user's input as-is so they can see what's wrong and fix it
                }
            }
        };

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
                    convertedValue = System.Convert.ChangeType(stringValues[i], elementType);
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
}