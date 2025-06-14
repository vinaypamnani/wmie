using System.Management;
using System.Windows;
using WmiExplorer.Presentation.ViewModels.Dialogs;

namespace WmiExplorer.Presentation.Views.Dialogs;

/// <summary>
/// Reusable dialog for editing WMI object properties.
/// Can be used for method parameters or instance editing.
/// </summary>
public partial class PropertyEditorDialog : Window
{
    /// <summary>
    /// Creates a PropertyEditorDialog for editing instance properties.
    /// </summary>
    public PropertyEditorDialog(ManagementBaseObject managementObject, string? title = null)
    {
        InitializeComponent();
        ViewModel = new PropertyEditorDialogViewModel(this, managementObject, title);
        DataContext = ViewModel;
    }

    /// <summary>
    /// Gets the cleaned result object after the dialog closes with OK.
    /// </summary>
    public ManagementBaseObject? Result => ViewModel.Result;

    public PropertyEditorDialogViewModel ViewModel { get; }

    /// <summary>
    /// Shows the dialog and returns the edited instance object if OK was clicked.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <returns>The edited ManagementBaseObject if OK was clicked, null if cancelled</returns>
    public static ManagementBaseObject? ShowEditor(Window owner, ManagementBaseObject managementObject, string? title = null)
    {
        var dialog = new PropertyEditorDialog(managementObject, title)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
}