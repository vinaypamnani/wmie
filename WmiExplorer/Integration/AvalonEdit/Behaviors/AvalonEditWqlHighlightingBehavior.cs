using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Xml;
using WmiExplorer.Common.Messages;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.AvalonEdit.Behaviors;

/// <summary>
/// Applies WQL syntax highlighting to AvalonEdit using an attached property and responds to theme changes.
/// </summary>
public static class AvalonEditWqlHighlightingBehavior
{
    public static readonly DependencyProperty EnableWqlHighlightingProperty = DependencyProperty.RegisterAttached(
        "EnableWqlHighlighting",
        typeof(bool),
        typeof(AvalonEditWqlHighlightingBehavior),
        new PropertyMetadata(false, OnEnableWqlHighlightingChanged));

    // Private static fields for injected services
    private static IMessengerService? _messengerService;

    private static ISettingsService? _settingsService;

    // Attached property to store the last applied theme for each editor
    private static readonly DependencyProperty LastAppliedThemeProperty = DependencyProperty.RegisterAttached(
        "LastAppliedTheme", typeof(string), typeof(AvalonEditWqlHighlightingBehavior), new PropertyMetadata(null));

    public static bool GetEnableWqlHighlighting(DependencyObject obj) => (bool)obj.GetValue(EnableWqlHighlightingProperty);

    public static void SetEnableWqlHighlighting(DependencyObject obj, bool value) => obj.SetValue(EnableWqlHighlightingProperty, value);

    /// <summary>
    /// Sets the messaging service for this behavior (for DI).
    /// </summary>
    public static void SetMessengerService(IMessengerService messengerService)
    {
        _messengerService = messengerService;
    }

    /// <summary>
    /// Sets the settings service for this behavior (for DI).
    /// </summary>
    public static void SetSettingsService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private static void ApplyWqlHighlighting(TextEditor editor, string? theme = null)
    {
        // Prevent unnecessary reload if the requested theme is already applied
        var lastTheme = GetLastAppliedTheme(editor);
        if (string.Equals(lastTheme, theme, StringComparison.OrdinalIgnoreCase) && editor.SyntaxHighlighting != null)
        {
            return;
        }

        IHighlightingDefinition? wqlHighlighting = null;
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "WmiExplorer.Resources.WqlHighlightLight.xshd";
            if (!string.IsNullOrEmpty(theme))
            {
                if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                    resourceName = "WmiExplorer.Resources.WqlHighlightDark.xshd";
                else if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                    resourceName = "WmiExplorer.Resources.WqlHighlightLight.xshd";
            }
            using (Stream? s = assembly.GetManifestResourceStream(resourceName))
            {
                if (s != null)
                {
                    using (XmlReader reader = new XmlTextReader(s))
                    {
                        wqlHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                        System.Diagnostics.Debug.WriteLine($"[AvalonEditHighlighting] Loaded {resourceName} for theme {theme ?? "default"}.");
                    }
                }
            }
        }
        catch
        {
            // Fallback to SQL highlighting if custom not found
            System.Diagnostics.Debug.WriteLine("[AvalonEditHighlighting] WQL highlighting not found.");
        }
        editor.SyntaxHighlighting = wqlHighlighting ?? HighlightingManager.Instance.GetDefinition("SQL");
        SetLastAppliedTheme(editor, theme);
    }

    private static string? GetLastAppliedTheme(TextEditor editor)
    {
        return (string?)editor.GetValue(LastAppliedThemeProperty);
    }

    private static void OnEnableWqlHighlightingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextEditor editor && (bool)e.NewValue)
        {
            // Get current theme from settings service via DI
            // string theme = _settingsService?.CurrentTheme ?? "Dark";
            // ApplyWqlHighlighting(editor, theme);

            // Subscribe to theme change messages via DI
            if (_messengerService != null)
            {
                _messengerService.StrongSubscribe<ThemeChangedMessage>(editor, msg =>
                {
                    ApplyWqlHighlighting(editor, msg.Theme);
                });
            }
        }
    }

    private static void SetLastAppliedTheme(TextEditor editor, string? value)
    {
        editor.SetValue(LastAppliedThemeProperty, value);
    }
}