using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using WmiExplorer.Common.Base;

namespace WmiExplorer.Presentation.Themes;

public partial class Theme : DisposableObservableObject
{
    [ObservableProperty]
    private Dictionary<string, Color> _themeColors = new Dictionary<string, Color>();

    [ObservableProperty]
    private string _themeName = string.Empty;

    public Theme(string name)
    {
        ThemeName = name;
        ThemeBrushes = new Dictionary<string, SolidColorBrush>();
    }

    public Dictionary<string, SolidColorBrush> ThemeBrushes { get; private set; }

    public Color this[string key]
    {
        get => ThemeColors.TryGetValue(key, out var color) ? color : Colors.Transparent;
        set
        {
            if (!ThemeColors.ContainsKey(key) || ThemeColors[key] != value)
            {
                ThemeColors[key] = value;
                // Auto-generate SecondaryAccentColor if PrimaryAccentColor is changed and not Transparent
                if (key == "PrimaryAccentColor" && value != Colors.Transparent)
                {
                    ThemeColors["SecondaryAccentColor"] = GenerateSecondaryAccentColor(value, ThemeColors);
                }
                OnThemeColorsChanged(ThemeColors);
                OnPropertyChanged($"Item[{key}]");
            }
        }
    }

    public static Dictionary<string, SolidColorBrush> CreateThemeBrushes(Dictionary<string, Color> colors)
    {
        // Helper to get a color or fallback
        Color GetColor(string key, string? fallbackKey = null, Color? fallbackColor = null)
        {
            if (colors.TryGetValue(key, out var c)) return c;
            if (fallbackKey != null && colors.TryGetValue(fallbackKey, out var fc)) return fc;
            return fallbackColor ?? Colors.Transparent;
        }

        var brushes = new Dictionary<string, SolidColorBrush>
        {
            // Base color brushes
            ["BaseGreenBrush"] = new SolidColorBrush(GetColor("BaseGreen", fallbackColor: Colors.Green)),
            ["BaseRedBrush"] = new SolidColorBrush(GetColor("BaseRed", fallbackColor: Colors.Red)),
            ["BaseOrangeBrush"] = new SolidColorBrush(GetColor("BaseOrange", fallbackColor: Colors.Orange)),
            ["BaseGrayBrush"] = new SolidColorBrush(GetColor("BaseGray", fallbackColor: Colors.Gray)),
            ["BaseBlueBrush"] = new SolidColorBrush(GetColor("BaseBlue", fallbackColor: Colors.Blue)),

            // Background brushes
            ["PrimaryBackgroundBrush"] = new SolidColorBrush(GetColor("PrimaryBackgroundColor", fallbackColor: Colors.White)),
            ["SecondaryBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
            ["TertiaryBackgroundBrush"] = new SolidColorBrush(GetColor("TertiaryBackgroundColor", fallbackColor: Colors.LightGray)),
            ["DisabledBackgroundBrush"] = new SolidColorBrush(GetColor("DisabledBackgroundColor", fallbackColor: Colors.LightGray)),

            // Foreground brushes
            ["PrimaryForegroundBrush"] = new SolidColorBrush(GetColor("PrimaryForegroundColor", fallbackColor: Colors.Black)),
            ["SecondaryForegroundBrush"] = new SolidColorBrush(GetColor("SecondaryForegroundColor", fallbackKey: "PrimaryForegroundColor", fallbackColor: Colors.Black)),
            ["TertiaryForegroundBrush"] = new SolidColorBrush(GetColor("TertiaryForegroundColor", fallbackKey: "SecondaryForegroundColor", fallbackColor: Colors.Gray)),
            ["DisabledForegroundBrush"] = new SolidColorBrush(GetColor("DisabledForegroundColor", fallbackColor: Colors.Gray)),

            // Accent brushes with improved contrast and interaction states
            ["PrimaryAccentBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)),
            ["SecondaryAccentBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackColor: Colors.MediumPurple)),

            // Border brush
            ["BorderBrush"] = new SolidColorBrush(GetColor("BorderColor", fallbackColor: Colors.DarkGray)),

            // Selection and interaction states with improved opacity for better contrast
            ["SelectedItemBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.8 },
            ["SelectedUnfocusedBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackKey: "PrimaryAccentColor", fallbackColor: Colors.LightBlue)) { Opacity = 0.4 },
            ["HoverBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.6 },
            ["ItemPressedBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackColor: Colors.MediumPurple)) { Opacity = 0.75 },

            // ScrollBar brushes with improved contrast and interaction states
            ["ScrollBarTrackBrush"] = new SolidColorBrush(GetColor("SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
            ["ScrollBarThumbBrush"] = new SolidColorBrush(GetColor("BorderColor", fallbackColor: Colors.DarkGray)) { Opacity = 0.8 },
            ["ScrollBarThumbHoverBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.7 },
            ["ScrollBarThumbPressedBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.9 },
        };

        // PropertyGrid* brushes are aliases to existing brushes
        brushes["PropertyGridBackgroundBrush"] = brushes["PrimaryBackgroundBrush"];
        brushes["PropertyGridForegroundBrush"] = brushes["PrimaryForegroundBrush"];
        brushes["PropertyGridSecondaryBackgroundBrush"] = brushes["TertiaryBackgroundBrush"];
        brushes["PropertyGridCategoryBackgroundBrush"] = brushes["SecondaryBackgroundBrush"];
        brushes["PropertyGridBorderBrush"] = brushes["BorderBrush"];
        brushes["PropertyGridAccentBrush"] = brushes["PrimaryAccentBrush"];
        brushes["PropertyGridSelectedBackgroundBrush"] = brushes["SelectedItemBackgroundBrush"];
        brushes["PropertyGridDisabledForegroundBrush"] = brushes["DisabledForegroundBrush"];
        brushes["PropertyGridHoverBackgroundBrush"] = brushes["HoverBackgroundBrush"];
        brushes["PropertyGridKeyHighlightBrush"] = brushes["BaseGreenBrush"];

        return brushes;
    }

    public void RegenerateBrushes()
    {
        ThemeBrushes = CreateThemeBrushes(ThemeColors);
    }

    // Darken a color by a percentage
    private static Color DarkenColor(Color color, double amount)
    {
        return Color.FromArgb(
            255,
            (byte)(color.R * (1 - amount)),
            (byte)(color.G * (1 - amount)),
            (byte)(color.B * (1 - amount))
        );
    }

    // Generates a secondary accent color based on the primary and current theme background
    private static Color GenerateSecondaryAccentColor(Color primary, Dictionary<string, Color> themeColors)
    {
        // Check if dark theme based on background color luminance
        Color bg = themeColors.TryGetValue("PrimaryBackgroundColor", out var b) ? b : Colors.White;
        bool isDarkTheme = GetLuminance(bg) < 0.5;

        if (isDarkTheme)
        {
            // For dark theme, make secondary accent lighter than primary
            return LightenColor(primary, 0.1);
        }
        else
        {
            // For light theme, make secondary accent slightly darker
            return DarkenColor(primary, 0.1);
        }
    }

    // Calculate color luminance (brightness)
    private static double GetLuminance(Color color)
    {
        return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
    }

    // Lighten a color by a percentage
    private static Color LightenColor(Color color, double amount)
    {
        return Color.FromArgb(
            255,
            (byte)Math.Min(255, color.R + (255 - color.R) * amount),
            (byte)Math.Min(255, color.G + (255 - color.G) * amount),
            (byte)Math.Min(255, color.B + (255 - color.B) * amount)
        );
    }

    /// <summary>    /// <summary>
    /// Called when ThemeColors property changes
    /// </summary>
    partial void OnThemeColorsChanged(Dictionary<string, Color> value)
    {
        RegenerateBrushes();
    }
}