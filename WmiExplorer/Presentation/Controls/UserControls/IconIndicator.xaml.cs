using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WmiExplorer.Common.Enums;

namespace WmiExplorer.Presentation.Controls.UserControls;

public partial class IconIndicator : UserControl
{
    public static readonly DependencyProperty AppStateProperty =
        DependencyProperty.Register(
            nameof(AppState),
            typeof(AppState),
            typeof(IconIndicator),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ExceptionProperty =
        DependencyProperty.Register(
            nameof(Exception),
            typeof(Exception),
            typeof(IconIndicator),
            new PropertyMetadata(null, OnExceptionChanged));

    public IconIndicator()
    {
        InitializeComponent();
    }

    public AppState AppState
    {
        get => (AppState)GetValue(AppStateProperty);
        set => SetValue(AppStateProperty, value);
    }

    public Exception? Exception
    {
        get => (Exception?)GetValue(ExceptionProperty);
        set => SetValue(ExceptionProperty, value);
    }

    private static void OnExceptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IconIndicator control)
        {
            var exception = e.NewValue as Exception;
            control.UpdateTooltip(exception);
        }
    }

    private void UpdateTooltip(Exception? exception)
    {
        if (FindName("StatusEllipse") is System.Windows.Shapes.Ellipse statusEllipse)
        {
            if (exception != null)
            {
                var stackPanel = new StackPanel { MaxWidth = 600 };
                var headerText = new TextBlock
                {
                    Text = "Exception Details:",
                    FontWeight = FontWeights.Bold
                };
                stackPanel.Children.Add(headerText);
                var logText = new TextBlock
                {
                    Text = "See Log tab for more details",
                    Margin = new Thickness(0, 2, 0, 0),
                    FontStyle = FontStyles.Italic,
                    FontSize = 11
                };
                stackPanel.Children.Add(logText);
                var messageText = new TextBlock
                {
                    Text = exception.Message ?? "No message available",
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(messageText);
                var stackTraceText = new TextBlock
                {
                    Text = exception.StackTrace ?? "No stack trace available",
                    Margin = new Thickness(0, 4, 0, 0),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(stackTraceText);
                statusEllipse.ToolTip = new ToolTip { Content = stackPanel, Placement = PlacementMode.Top };
            }
            else
            {
                statusEllipse.ToolTip = null;
            }
        }
    }
}