using System.Windows;
using System.Windows.Controls;
using WmiExplorer.Common.Enums;

namespace WmiExplorer.Presentation.Controls.UserControls;

public partial class ProgressBarIndicator : UserControl
{
    public static readonly DependencyProperty AppStateProperty =
        DependencyProperty.Register(
            nameof(AppState),
            typeof(AppState),
            typeof(ProgressBarIndicator),
            new PropertyMetadata(null));

    public ProgressBarIndicator()
    {
        InitializeComponent();
    }

    public AppState AppState
    {
        get => (AppState)GetValue(AppStateProperty);
        set => SetValue(AppStateProperty, value);
    }
}