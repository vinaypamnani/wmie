using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WmiExplorer.Presentation.Controls.UserControls;

public partial class CollapsibleTextBlock : UserControl
{
    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(CollapsibleTextBlock),
            new PropertyMetadata(false));

    public static readonly DependencyProperty TextForegroundProperty =
        DependencyProperty.Register(nameof(TextForeground), typeof(Brush), typeof(CollapsibleTextBlock),
            new PropertyMetadata(null));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(CollapsibleTextBlock),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ToggleCommandProperty =
        DependencyProperty.Register(nameof(ToggleCommand), typeof(ICommand), typeof(CollapsibleTextBlock),
            new PropertyMetadata(null));

    public CollapsibleTextBlock()
    {
        InitializeComponent();
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush TextForeground
    {
        get => (Brush)GetValue(TextForegroundProperty);
        set => SetValue(TextForegroundProperty, value);
    }

    public ICommand ToggleCommand
    {
        get => (ICommand)GetValue(ToggleCommandProperty);
        set => SetValue(ToggleCommandProperty, value);
    }

    private void ToggleButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ToggleCommand?.CanExecute(null) == true)
        {
            ToggleCommand.Execute(null);
        }
        else
        {
            // Fallback: toggle the property directly
            IsExpanded = !IsExpanded;
        }
    }
}