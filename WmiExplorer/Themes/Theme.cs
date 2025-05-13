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
                    RegenerateBrushes();
                    OnPropertyChanged($"Item[{key}]");
                }
            }
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

            return new Dictionary<string, SolidColorBrush>
            {
                ["SuccessBrush"] = new SolidColorBrush(GetColor("SuccessColor", fallbackColor: Colors.Green)),
                ["ErrorBrush"] = new SolidColorBrush(GetColor("ErrorColor", fallbackColor: Colors.Red)),
                ["WarningBrush"] = new SolidColorBrush(GetColor("WarningColor", fallbackColor: Colors.Orange)),
                ["IndeterminateBrush"] = new SolidColorBrush(GetColor("IndeterminateColor", fallbackColor: Colors.Gray)),
                ["BusyBrush"] = new SolidColorBrush(GetColor("BusyColor", fallbackColor: Colors.Blue)),
                ["PrimaryBackgroundBrush"] = new SolidColorBrush(GetColor("PrimaryBackgroundColor", fallbackColor: Colors.White)),
                ["SecondaryBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
                ["TertiaryBackgroundBrush"] = new SolidColorBrush(GetColor("TertiaryBackgroundColor", fallbackKey: "SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
                ["PrimaryForegroundBrush"] = new SolidColorBrush(GetColor("PrimaryForegroundColor", fallbackColor: Colors.Black)),
                ["SecondaryForegroundBrush"] = new SolidColorBrush(GetColor("SecondaryForegroundColor", fallbackKey: "PrimaryForegroundColor", fallbackColor: Colors.Black)),
                ["ReadOnlyForegroundBrush"] = new SolidColorBrush(GetColor("ReadOnlyForegroundColor", fallbackKey: "PrimaryForegroundColor", fallbackColor: Colors.Gray)),
                ["PrimaryAccentBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)),
                ["SecondaryAccentBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackColor: Colors.MediumPurple)),
                ["BorderBrush"] = new SolidColorBrush(GetColor("BorderColor", fallbackColor: Colors.DarkGray)),
                ["SelectedItemBackgroundBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)),
                ["SelectedUnfocusedBackgroundBrush"] = new SolidColorBrush(GetColor("NoFocusColor", fallbackKey: "PrimaryAccentColor", fallbackColor: Colors.LightBlue)),
                ["HoverBackgroundBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.5 },
                ["ItemPressedBrush"] = new SolidColorBrush(GetColor("SecondaryAccentColor", fallbackColor: Colors.MediumPurple)) { Opacity = 0.5 },
                ["ScrollBarTrackBrush"] = new SolidColorBrush(GetColor("SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
                ["ScrollBarThumbBrush"] = new SolidColorBrush(GetColor("BorderColor", fallbackColor: Colors.DarkGray)),
                ["ScrollBarThumbHoverBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.5 },
                ["ScrollBarThumbPressedBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)),
                ["PropertyGridBackgroundBrush"] = new SolidColorBrush(GetColor("PrimaryBackgroundColor", fallbackColor: Colors.White)),
                ["PropertyGridForegroundBrush"] = new SolidColorBrush(GetColor("PrimaryForegroundColor", fallbackColor: Colors.Black)),
                ["PropertyGridSecondaryBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
                ["PropertyGridCategoryBackgroundBrush"] = new SolidColorBrush(GetColor("SecondaryBackgroundColor", fallbackColor: Colors.LightGray)),
                ["PropertyGridBorderBrush"] = new SolidColorBrush(GetColor("BorderColor", fallbackColor: Colors.DarkGray)),
                ["PropertyGridAccentBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)),
                ["PropertyGridSelectedBackgroundBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)),
                ["PropertyGridReadOnlyForegroundBrush"] = new SolidColorBrush(GetColor("ReadOnlyForegroundColor", fallbackKey: "PrimaryForegroundColor", fallbackColor: Colors.Gray)),
                ["PropertyGridHoverBackgroundBrush"] = new SolidColorBrush(GetColor("PrimaryAccentColor", fallbackColor: Colors.DodgerBlue)) { Opacity = 0.5 },
            };
        }
    }

    public class ThemeCollection : Dictionary<string, Theme>
    {
        public ThemeCollection() : base(StringComparer.OrdinalIgnoreCase) { }
        public Theme? Default => this.ContainsKey("Dark") ? this["Dark"] : this.Values.FirstOrDefault();
    }
}
