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

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}