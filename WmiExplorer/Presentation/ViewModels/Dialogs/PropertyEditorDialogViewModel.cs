using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WmiExplorer.Common.Logging;

namespace WmiExplorer.Presentation.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the PropertyEditorDialog that allows editing WMI objects.
/// </summary>
public partial class PropertyEditorDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _editableObject;

    [ObservableProperty]
    private string _objectTypeName = string.Empty;

    private readonly ManagementBaseObject? _originalObject;

    [ObservableProperty]
    private string _title = "Edit Properties";

    private readonly Window _window;

    /// <summary>
    /// Initializes the dialog for editing a raw ManagementBaseObject (instance editing).
    /// </summary>
    public PropertyEditorDialogViewModel(Window window, ManagementBaseObject managementObject, string? title = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _originalObject = managementObject ?? throw new ArgumentNullException(nameof(managementObject));

        Title = title ?? _title;
        ObjectTypeName = managementObject.ClassPath?.ClassName ?? "Unknown";
        EditableObject = managementObject;
    }

    /// <summary>
    /// Gets the cleaned result object. Only available after OK is clicked.
    /// </summary>
    public ManagementBaseObject? Result { get; private set; }

    [RelayCommand]
    private void Cancel()
    {
        Result = null;
        _window.DialogResult = false;
        _window.Close();
    }

    /// <summary>
    /// Extracts the error message from the tooltip text.
    /// </summary>
    private string ExtractErrorMessageFromTooltip(string tooltip)
    {
        // Extract the actual error message from the tooltip format
        // Expected format: "❌ Validation Error: {message}\n\nPress Escape to reset to original value."

        var lines = tooltip.Split('\n');
        var errorLine = lines.FirstOrDefault(line => line.Contains("Validation Error:"));

        if (errorLine != null)
        {
            var index = errorLine.IndexOf("Validation Error:");
            if (index >= 0)
            {
                return errorLine.Substring(index + "Validation Error:".Length).Trim();
            }
        }

        return "Invalid value";
    }

    /// <summary>
    /// Attempts to find the property name associated with a TextBox.
    /// </summary>
    private string FindPropertyNameForTextBox(TextBox textBox)
    {
        // Try to find the property name by looking at the data context or nearby labels
        // This is a simplified approach - could be enhanced for better property identification

        var parent = textBox.Parent;
        while (parent != null)
        {
            if (parent is FrameworkElement fe && fe.DataContext != null)
            {
                // Check if the DataContext has a Name property (PropertyHierarchyItem)
                var nameProperty = fe.DataContext.GetType().GetProperty("Name");
                if (nameProperty != null)
                {
                    var name = nameProperty.GetValue(fe.DataContext)?.ToString();
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }

            parent = parent is FrameworkElement element ? element.Parent : null;
        }

        return "Property";
    }

    /// <summary>
    /// Finds validation errors by searching for TextBoxes with red borders in the visual tree.
    /// </summary>
    /// <returns>List of validation error messages</returns>
    private List<string> FindValidationErrors()
    {
        var errors = new List<string>();
        var textBoxes = FindVisualChildren<TextBox>(_window);

        foreach (var textBox in textBoxes)
        {
            // Check if the TextBox has a red border (indicating validation error)
            if (textBox.BorderBrush is SolidColorBrush brush && brush.Color == Colors.Red)
            {
                // Extract error message from tooltip
                var toolTip = textBox.ToolTip?.ToString();
                if (!string.IsNullOrEmpty(toolTip) && toolTip.Contains("Validation Error"))
                {
                    // Try to find a property name or use a generic description
                    var propertyName = FindPropertyNameForTextBox(textBox);
                    var errorMessage = ExtractErrorMessageFromTooltip(toolTip);

                    errors.Add($"• {propertyName}: {errorMessage}");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Finds all visual children of a specified type in the visual tree.
    /// </summary>
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T t)
                yield return t;

            foreach (var childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }

    [RelayCommand]
    private void Ok()
    {
        try
        {
            // Check for validation errors before closing
            var validationErrors = FindValidationErrors();
            if (validationErrors.Count > 0)
            {
                var errorMessage = "Please fix the following validation errors before continuing:\n\n" +
                                   string.Join("\n", validationErrors) +
                                   "\n\nTip: Press Escape in any error field to reset to original value.";

                MessageBox.Show(errorMessage, "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Don't close the dialog
            }

            // Get the cleaned result based on the object type
            if (_originalObject != null)
            {
                // For raw ManagementBaseObjects (instances), return as-is
                // Could add validation or cleaning logic here if needed
                Result = _originalObject;
            }

            _window.DialogResult = true;
            _window.Close();
        }
        catch (Exception ex)
        {
            // Could show error message to user
            Log.Error(ex, "Error processing properties in PropertyEditorDialog");
            MessageBox.Show($"Error processing properties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}