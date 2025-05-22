using System.Windows;
using ListViewItem = System.Windows.Controls.ListViewItem;
using System.Windows.Input;

namespace WmiExplorer.Presentation.Behaviors;

public static class ListViewItemDoubleClickBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(ListViewItemDoubleClickBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand GetCommand(DependencyObject element) =>
        (ICommand)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand value) =>
                                element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListViewItem item)
        {
            item.MouseDoubleClick -= OnMouseDoubleClick;
            if (e.NewValue != null)
                item.MouseDoubleClick += OnMouseDoubleClick;
        }
    }

    private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem item && item.IsSelected && GetCommand(item) is ICommand command)
        {
            if (item.DataContext is object context && command.CanExecute(context))
            {
                command.Execute(context);
                e.Handled = true;
            }
        }
    }
}