using System.Windows;
using WmiExplorer.Models;
using WmiExplorer.Presentation.ViewModels.Dialogs;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.Views.Dialogs;

/// <summary>
/// Interaction logic for MethodExecutionDialog.xaml
/// </summary>
public partial class MethodExecutionDialog : Window
{
    private readonly MethodExecutionDialogViewModel? _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodExecutionDialog"/> class.
    /// </summary>
    public MethodExecutionDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodExecutionDialog"/> class with a ViewModel.
    /// </summary>
    /// <param name="viewModel">The ViewModel to use for this dialog.</param>
    public MethodExecutionDialog(MethodExecutionDialogViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Subscribe to close requested event
        _viewModel.CloseRequested += (s, e) => DialogResult = false;
    }

    /// <summary>
    /// Shows the method execution dialog.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="wmiService">The WMI service for executing methods.</param>
    /// <param name="namespaceViewModel">The WMI namespace view model.</param>
    /// <param name="wmiClass">The WMI class.</param>
    /// <param name="wmiMethod">The WMI method to execute.</param>
    /// <param name="messengerService">The messenger service.</param>
    /// <param name="wmiInstance">The WMI instance (if non-static method).</param>
    /// <returns>True if the dialog was closed successfully, false otherwise.</returns>
    public static bool ShowDialog(
        Window owner,
        IWmiService wmiService,
        WmiNamespaceViewModel namespaceViewModel,
        WmiClass wmiClass,
        WmiMethod wmiMethod,
        IMessengerService messengerService,
        WmiInstance? wmiInstance = null)
    {
        var viewModel = new MethodExecutionDialogViewModel(
            wmiService,
            namespaceViewModel,
            wmiClass,
            wmiMethod,
            messengerService,
            wmiInstance);
        var dialog = new MethodExecutionDialog(viewModel)
        {
            Owner = owner
        };
        return dialog.ShowDialog() ?? false;
    }
}