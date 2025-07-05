using System.Windows;
using System.Windows.Controls;
using WmiExplorer.Common.Enums;

namespace WmiExplorer.Presentation.Controls.UserControls;

/// <summary>
/// Enhanced status indicator that combines the visual state indicator and status message
/// </summary>
public partial class EnhancedStatusIndicator : UserControl
{
    public static readonly DependencyProperty AppStateProperty =
        DependencyProperty.Register(
            nameof(AppState),
            typeof(AppState),
            typeof(EnhancedStatusIndicator),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ExceptionProperty =
        DependencyProperty.Register(
            nameof(Exception),
            typeof(Exception),
            typeof(EnhancedStatusIndicator),
            new PropertyMetadata(null, OnExceptionChanged));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(EnhancedStatusIndicator),
            new PropertyMetadata(string.Empty));

    public EnhancedStatusIndicator()
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

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private static void OnExceptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EnhancedStatusIndicator control)
        {
            var exception = e.NewValue as Exception;

            // Update the tooltip programmatically
            control.UpdateTooltip(exception);
        }
    }

    private void UpdateTooltip(Exception? exception)
    {
        // Find the error ellipse by name
        if (FindName("ErrorEllipse") is System.Windows.Shapes.Ellipse errorEllipse)
        {
            if (exception != null)
            {
                var stackPanel = new System.Windows.Controls.StackPanel { MaxWidth = 600 };

                var headerText = new System.Windows.Controls.TextBlock
                {
                    Text = "Exception Details:",
                    FontWeight = System.Windows.FontWeights.Bold
                };
                stackPanel.Children.Add(headerText);

                var logText = new System.Windows.Controls.TextBlock
                {
                    Text = "See Log tab for more details",
                    Margin = new System.Windows.Thickness(0, 2, 0, 0),
                    FontStyle = System.Windows.FontStyles.Italic,
                    FontSize = 11
                };
                stackPanel.Children.Add(logText);

                var messageText = new System.Windows.Controls.TextBlock
                {
                    Text = exception.Message ?? "No message available",
                    Margin = new System.Windows.Thickness(0, 4, 0, 0),
                    TextWrapping = System.Windows.TextWrapping.Wrap
                };
                stackPanel.Children.Add(messageText);

                var stackTraceText = new System.Windows.Controls.TextBlock
                {
                    Text = exception.StackTrace ?? "No stack trace available",
                    Margin = new System.Windows.Thickness(0, 4, 0, 0),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10,
                    TextWrapping = System.Windows.TextWrapping.Wrap
                };
                stackPanel.Children.Add(stackTraceText);

                errorEllipse.ToolTip = new System.Windows.Controls.ToolTip { Content = stackPanel };
            }
            else
            {
                errorEllipse.ToolTip = null;
            }
        }
    }
}