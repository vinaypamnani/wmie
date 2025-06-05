using System.Windows;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels;

namespace WmiExplorer.Presentation.Views.Dialogs;

/// <summary>
/// Interaction logic for MethodExecutionDialog.xaml
/// </summary>
public partial class MethodExecutionDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MethodExecutionDialog"/> class.
    /// </summary>
    public MethodExecutionDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the method execution dialog.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="wmiNamespace">The WMI namespace.</param>
    /// <param name="wmiClass">The WMI class.</param>
    /// <param name="wmiMethod">The WMI method to execute.</param>
    /// <param name="wmiInstance">The WMI instance (if non-static method).</param>
    /// <returns>True if the dialog was closed successfully, false otherwise.</returns>
    public static bool ShowDialog(
        Window owner,
        WmiNamespace wmiNamespace,
        WmiClass wmiClass,
        WmiMethod wmiMethod,
        WmiInstance? wmiInstance = null)
    {
        var dialog = new MethodExecutionDialog
        {
            Owner = owner,
            DataContext = new MethodExecutionViewModel(
                wmiNamespace,
                wmiClass,
                wmiMethod,
                wmiInstance)
        };

        return dialog.ShowDialog() ?? false;
    }
}