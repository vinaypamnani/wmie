using System.Collections.Generic;
using System.Windows.Media;
using WmiExplorer.Common.Base;

namespace WmiExplorer.Themes
{
    public class Theme : ViewModelBase
    {
        public string ThemeName = string.Empty;
        private Dictionary<string, Color> _themeColors = new Dictionary<string, Color>();
        public Dictionary<string, Color> ThemeColors
        {
            get => _themeColors;
            set
            {
                _themeColors = value;
                RegenerateBrushes();
            }
        }
        public Dictionary<string, SolidColorBrush> ThemeBrushes { get; private set; }

        private void RegenerateBrushes()
        {
            ThemeBrushes = CreateThemeBrushes(_themeColors);
        }

        public Color this[string key]
        {
            get => ThemeColors.TryGetValue(key, out var color) ? color : Colors.Transparent;
            set
            {
                if (!ThemeColors.ContainsKey(key) || ThemeColors[key] != value)
                {
                    ThemeColors[key] = value;
                    // Auto-generate SecondaryAccentColor if PrimaryAccentColor is changed
                    if (key == "PrimaryAccentColor")
                    {
                        ThemeColors["SecondaryAccentColor"] = GenerateSecondaryAccentColor(value, ThemeColors);
                    }
                    RegenerateBrushes();
                    OnPropertyChanged($"Item[{key}]");
                }
            }
        }

        // Generates a secondary accent color based on the primary and current theme background
        private static Color GenerateSecondaryAccentColor(Color primary, Dictionary<string, Color> themeColors)
        {
            // Try to get background color for contrast
            Color bg = themeColors.TryGetValue("PrimaryBackgroundColor", out var b) ? b : Colors.White;
            // Simple algorithm: blend primary with background (60% primary, 40% bg)
            byte Blend(byte a, byte b, double t) => (byte)(a * t + b * (1 - t));
            return Color.FromArgb(
                255,
                Blend(primary.R, bg.R, 0.6),
                Blend(primary.G, bg.G, 0.6),
                Blend(primary.B, bg.B, 0.6)
            );
        }

        public Theme(string name)
        {
            ThemeName = name;
            ThemeBrushes = new Dictionary<string, SolidColorBrush>();
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
                ["SuccessBrush"] = new SolidColorBrush(GetColor("SuccessColor", fallbackColor: Colors.Green)),
                ["ErrorBrush"] = new SolidColorBrush(GetColor("ErrorColor", fallbackColor: Colors.Red)),
                ["WarningBrush"] = new SolidColorBrush(GetColor("WarningColor", fallbackColor: Colors.Orange)),
                ["IndeterminateBrush"] = new SolidColorBrush(GetColor("IndeterminateColor", fallbackColor: Colors.Gray)),
                ["BusyBrush"] = new SolidColorBrush(GetColor("BusyColor", fallbackColor: Colors.Blue)),
                ["PrimaryBackgroundBrush"] = new SolidColorBrush(GetColor("PrimaryBackgroundColor", fallbackColor: Colors.White)),
                ["SecondaryBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
                ["TertiaryBackgroundBrush"] = new SolidColorBrush(GetColor("TertiaryBackgroundColor", fallbackKey: "SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
                ["DisabledBackgroundBrush"] = new SolidColorBrush(GetColor("DisabledBackgroundColor", fallbackColor: Colors.LightGray)),
                ["PrimaryForegroundBrush"] = new SolidColorBrush(GetColor("PrimaryForegroundColor", fallbackColor: Colors.Black)),
                ["SecondaryForegroundBrush"] = new SolidColorBrush(GetColor("SecondaryForegroundColor", fallbackKey: "PrimaryForegroundColor", fallbackColor: Colors.Black)),                
                ["DisabledForegroundBrush"] = new SolidColorBrush(GetColor("DisabledForegroundColor", fallbackColor: Colors.Gray)),
                ["PrimaryAccentBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)),
                ["SecondaryAccentBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackColor: Colors.MediumPurple)),
                ["BorderBrush"] = new SolidColorBrush(GetColor("BorderColor", fallbackColor: Colors.DarkGray)),
                ["SelectedItemBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.7 },
                ["SelectedUnfocusedBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackKey: "PrimaryAccentColor", fallbackColor: Colors.LightBlue)) { Opacity = 0.3},
                ["HoverBackgroundBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.5 },
                ["ItemPressedBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackColor: Colors.MediumPurple)) { Opacity = 0.5 },
                ["ScrollBarTrackBrush"] = new SolidColorBrush(GetColor("SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
                ["ScrollBarThumbBrush"] = new SolidColorBrush(GetColor("BorderColor", fallbackColor: Colors.DarkGray)),
                ["ScrollBarThumbHoverBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.5 },
                ["ScrollBarThumbPressedBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)),                
            };

            // PropertyGrid* brushes are aliases to existing brushes
            brushes["PropertyGridBackgroundBrush"] = brushes["PrimaryBackgroundBrush"];
            brushes["PropertyGridForegroundBrush"] = brushes["PrimaryForegroundBrush"];
            brushes["PropertyGridSecondaryBackgroundBrush"] = brushes["SecondaryBackgroundBrush"];
            brushes["PropertyGridCategoryBackgroundBrush"] = brushes["SecondaryBackgroundBrush"];
            brushes["PropertyGridBorderBrush"] = brushes["BorderBrush"];
            brushes["PropertyGridAccentBrush"] = brushes["PrimaryAccentBrush"];
            brushes["PropertyGridSelectedBackgroundBrush"] = brushes["SelectedItemBackgroundBrush"];            
            brushes["PropertyGridDisabledForegroundBrush"] = brushes["DisabledForegroundBrush"];
            brushes["PropertyGridHoverBackgroundBrush"] = brushes["HoverBackgroundBrush"];

            return brushes;
        }
    }

    public class ThemeCollection : Dictionary<string, Theme>
    {
        public ThemeCollection() : base(StringComparer.OrdinalIgnoreCase) { }
        public Theme? Default => this.ContainsKey("Dark") ? this["Dark"] : this.Values.FirstOrDefault();
    }
}
