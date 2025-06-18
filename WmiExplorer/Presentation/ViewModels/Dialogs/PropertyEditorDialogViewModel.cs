using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Management;
using System.Windows;
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

    [RelayCommand]
    private void Ok()
    {
        try
        {
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