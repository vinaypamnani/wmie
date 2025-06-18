using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using WmiExplorer.Common.Logging;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Provides an attached property to enable automatic hyperlink navigation in the default browser.
/// </summary>
public static class HyperlinkNavigationBehavior
{
    /// <summary>
    /// Attached property to enable hyperlink navigation for any FrameworkElement containing hyperlinks.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(HyperlinkNavigationBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// Gets the value of the IsEnabled attached property.
    /// </summary>
    /// <param name="obj">The dependency object.</param>
    /// <returns>True if hyperlink navigation is enabled, false otherwise.</returns>
    public static bool GetIsEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Sets the value of the IsEnabled attached property.
    /// </summary>
    /// <param name="obj">The dependency object.</param>
    /// <param name="value">True to enable hyperlink navigation, false to disable.</param>
    public static void SetIsEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Handles hyperlink navigation to open URLs in the default browser.
    /// </summary>
    private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            // Silently fail - don't show error to user for link clicks but log the error
            Log.Warning(ex, "Failed to open hyperlink: {Uri}", e.Uri.AbsoluteUri);
        }
    }

    /// <summary>
    /// Called when the IsEnabled property changes.
    /// </summary>
    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            if ((bool)e.NewValue)
            {
                // Enable hyperlink navigation by adding the event handler
                element.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(Hyperlink_RequestNavigate));
            }
            else
            {
                // Disable hyperlink navigation by removing the event handler
                element.RemoveHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(Hyperlink_RequestNavigate));
            }
        }
    }
}