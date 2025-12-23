using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Behavior that enables double-click command execution for TreeViewItem and ListViewItem controls
/// </summary>
public static class ItemDoubleClickBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(ItemDoubleClickBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand GetCommand(DependencyObject element) =>
        (ICommand)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand value) =>
        element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Support both TreeViewItem and ListViewItem
        if (d is TreeViewItem or ListViewItem)
        {
            var control = (Control)d;

            // Remove existing handler
            control.MouseDoubleClick -= OnMouseDoubleClick;

            // Add handler if command is not null
            if (e.NewValue != null)
            {
                control.MouseDoubleClick += OnMouseDoubleClick;
            }
        }
    }

    private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var control = (Control)sender;
        var command = GetCommand(control);

        if (command == null || control.DataContext is not object context)
            return;

        // Check if item is selected based on control type
        var isSelected = sender switch
        {
            TreeViewItem treeItem => treeItem.IsSelected,
            ListViewItem listItem => listItem.IsSelected,
            _ => false
        };

        if (isSelected && command.CanExecute(context))
        {
            command.Execute(context);
            e.Handled = true;
        }
    }
}