using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WmiExplorer.Presentation.Behaviors;

public static class TreeViewItemDoubleClickBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(TreeViewItemDoubleClickBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand GetCommand(DependencyObject element)
    {
        return (ICommand)element.GetValue(CommandProperty)!;
    }

    public static void SetCommand(DependencyObject element, ICommand value)
    {
        element.SetValue(CommandProperty, value);
    }

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreeViewItem item)
        {
            if (e.OldValue == null && e.NewValue != null)
            {
                item.MouseDoubleClick += OnMouseDoubleClick;
            }
            else if (e.OldValue != null && e.NewValue == null)
            {
                item.MouseDoubleClick -= OnMouseDoubleClick;
            }
        }
    }

    private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && item.IsSelected && GetCommand(item) is ICommand command)
        {
            if (item.DataContext is object context && command.CanExecute(context))
            {
                command.Execute(context);
                e.Handled = true;
            }
        }
    }
}