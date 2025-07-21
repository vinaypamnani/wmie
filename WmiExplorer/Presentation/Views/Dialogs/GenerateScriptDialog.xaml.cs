using System.Management;
using System.Windows;
using WmiExplorer.Presentation.ViewModels.Dialogs;

namespace WmiExplorer.Presentation.Views.Dialogs;

/// <summary>
/// Dialog for generating PowerShell scripts for WMI operations.
/// </summary>
public partial class GenerateScriptDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the GenerateScriptDialog.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="selectedItem">The WMI item to generate script for (WmiClass, WmiInstance, or WmiMethod)</param>
    /// <param name="managementScope">The WMI management scope</param>
    public GenerateScriptDialog(Window owner, object selectedItem, ManagementScope managementScope)
        : this(owner, selectedItem, managementScope, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the GenerateScriptDialog with parameter values.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="selectedItem">The WMI item to generate script for (WmiClass, WmiInstance, or WmiMethod)</param>
    /// <param name="managementScope">The WMI management scope</param>
    /// <param name="parameterValues">Dictionary of parameter names and their values (for methods)</param>
    public GenerateScriptDialog(Window owner, object selectedItem, ManagementScope managementScope, Dictionary<string, object>? parameterValues)
    {
        InitializeComponent();
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ViewModel = new GenerateScriptDialogViewModel(this, selectedItem, managementScope, parameterValues);
        DataContext = ViewModel;
    }

    /// <summary>
    /// Gets the ViewModel for this dialog.
    /// </summary>
    public GenerateScriptDialogViewModel ViewModel { get; }

    /// <summary>
    /// Shows the GenerateScriptDialog.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="selectedItem">The WMI item to generate script for</param>
    /// <param name="managementScope">The WMI management scope</param>
    /// <returns>True if the dialog was closed successfully, false otherwise</returns>
    public static bool ShowDialog(Window owner, object selectedItem, ManagementScope managementScope)
    {
        var dialog = new GenerateScriptDialog(owner, selectedItem, managementScope);
        return dialog.ShowDialog() ?? false;
    }

    /// <summary>
    /// Shows the GenerateScriptDialog with parameter values.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="selectedItem">The WMI item to generate script for</param>
    /// <param name="managementScope">The WMI management scope</param>
    /// <param name="parameterValues">Dictionary of parameter names and their values (for methods)</param>
    /// <returns>True if the dialog was closed successfully, false otherwise</returns>
    public static bool ShowDialog(Window owner, object selectedItem, ManagementScope managementScope, Dictionary<string, object>? parameterValues)
    {
        var dialog = new GenerateScriptDialog(owner, selectedItem, managementScope, parameterValues);
        return dialog.ShowDialog() ?? false;
    }
}