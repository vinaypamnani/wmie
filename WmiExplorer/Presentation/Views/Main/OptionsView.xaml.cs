using System.Windows;
using System.Windows.Controls;

namespace WmiExplorer.Presentation.Views.Main;

/// <summary>
/// Interaction logic for OptionsView.xaml
/// </summary>
public partial class OptionsView : UserControl
{
    public OptionsView()
    {
        InitializeComponent();
    }

    public void FocusComputerNameTextBox()
    {
        ComputerNameTextBox.Focus();
        if (ComputerNameTextBox.Text.Length > 0)
            ComputerNameTextBox.CaretIndex = ComputerNameTextBox.Text.Length;
    }

    /// <summary>
    /// Handles the dropdown button click to show the context menu
    /// </summary>
    private void ConnectDropdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }
}