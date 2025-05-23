using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WmiExplorer.Presentation.ColorPicker;

/// <summary>
/// A control that allows users to pick colors using a color picker dialog.
/// </summary>
public class ColorPicker : Control, INotifyPropertyChanged
{
    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public static readonly DependencyProperty ShowAlphaChannelProperty =
        DependencyProperty.Register(
            nameof(ShowAlphaChannel),
            typeof(bool),
            typeof(ColorPicker),
            new PropertyMetadata(false, OnShowAlphaChannelChanged));

    public static readonly DependencyProperty ShowRecentColorsProperty =
        DependencyProperty.Register(
            nameof(ShowRecentColors),
            typeof(bool),
            typeof(ColorPicker),
            new PropertyMetadata(true));

    private readonly ObservableCollection<SolidColorBrush> _availableColors = new ObservableCollection<SolidColorBrush>();
    private Button? _cancelButton;

    // Template parts
    private Button? _colorButton;

    private Popup? _colorPickerPopup;
    private double _hue;
    private HueSlider? _hueSlider;
    private bool _isUpdatingValues = false;
    private Button? _okButton;
    private readonly ObservableCollection<SolidColorBrush> _recentColors = new ObservableCollection<SolidColorBrush>();
    private double _saturation;
    private SaturationValueCanvas? _saturationValueCanvas;
    private Color _selectedColor = Colors.Black;
    private readonly ObservableCollection<SolidColorBrush> _standardColors = new ObservableCollection<SolidColorBrush>();
    private Color _tempColor = Colors.Black;
    private double _value;

    /// <summary>
    /// Initializes a new instance of the ColorPicker control.
    /// </summary>
    public ColorPicker()
    {
        // Initialize command
        SelectColorCommand = new RelayCommand<SolidColorBrush>(OnSelectColor);

        // Initialize standard and available colors
        InitializeStandardColors();
        InitializeAvailableColors();

        // Initialize HSV from current RGB
        UpdateHSVFromRGB();
    }

    static ColorPicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(typeof(ColorPicker)));
    }

    /// <summary>
    /// Gets or sets the alpha component of the color (0-255).
    /// </summary>
    public byte Alpha
    {
        get => SelectedColor.A;
        set
        {
            if (!_isUpdatingValues)
            {
                _isUpdatingValues = true;
                Color newColor = SelectedColor;
                newColor.A = value;
                SelectedColor = newColor;
                _isUpdatingValues = false;
            }
            OnPropertyChanged(nameof(Alpha));
        }
    }

    /// <summary>
    /// Gets a collection of available colors (all from System.Windows.Media.Colors except Transparent).
    /// </summary>
    public ObservableCollection<SolidColorBrush> AvailableColors => _availableColors;

    /// <summary>
    /// Gets or sets the blue component of the color (0-255).
    /// </summary>
    public byte Blue
    {
        get => SelectedColor.B;
        set
        {
            if (!_isUpdatingValues)
            {
                _isUpdatingValues = true;
                Color newColor = SelectedColor;
                newColor.B = value;
                SelectedColor = newColor;
                UpdateHSVFromRGB();
                _isUpdatingValues = false;
            }
            OnPropertyChanged(nameof(Blue));
        }
    }

    /// <summary>
    /// Gets or sets the green component of the color (0-255).
    /// </summary>
    public byte Green
    {
        get => SelectedColor.G;
        set
        {
            if (!_isUpdatingValues)
            {
                _isUpdatingValues = true;
                Color newColor = SelectedColor;
                newColor.G = value;
                SelectedColor = newColor;
                UpdateHSVFromRGB();
                _isUpdatingValues = false;
            }
            OnPropertyChanged(nameof(Green));
        }
    }

    /// <summary>
    /// Gets or sets the hex representation of the color.
    /// </summary>
    public string HexColor
    {
        get => SelectedColor.ToString();
        set
        {
            try
            {
                if (value.StartsWith("#"))
                {
                    var color = (Color)ColorConverter.ConvertFromString(value);
                    SelectedColor = color;
                }
                else if (value.Length == 6 || value.Length == 8)
                {
                    var color = (Color)ColorConverter.ConvertFromString("#" + value);
                    SelectedColor = color;
                }
            }
            catch
            {
                // Invalid hex color - ignore
            }
            OnPropertyChanged(nameof(HexColor));
        }
    }

    /// <summary>
    /// Gets or sets the hue component of the color (0-360).
    /// </summary>
    public double Hue
    {
        get => Math.Round(_hue, 10);
        set
        {
            if (_hue != value && !_isUpdatingValues)
            {
                _isUpdatingValues = true;
                _hue = value;
                UpdateRGBFromHSV();
                _isUpdatingValues = false;
            }
            OnPropertyChanged(nameof(Hue));
        }
    }

    /// <summary>
    /// Gets a collection of recent colors.
    /// </summary>
    public ObservableCollection<SolidColorBrush> RecentColors => _recentColors;

    /// <summary>
    /// Gets or sets the red component of the color (0-255).
    /// </summary>
    public byte Red
    {
        get => SelectedColor.R;
        set
        {
            if (!_isUpdatingValues)
            {
                _isUpdatingValues = true;
                Color newColor = SelectedColor;
                newColor.R = value;
                SelectedColor = newColor;
                UpdateHSVFromRGB();
                _isUpdatingValues = false;
            }
            OnPropertyChanged(nameof(Red));
        }
    }

    /// <summary>
    /// Gets or sets the saturation component of the color (0-100).
    /// </summary>
    public double Saturation
    {
        get => Math.Round(_saturation, 10);
        set
        {
            if (_saturation != value && !_isUpdatingValues)
            {
                _isUpdatingValues = true;
                _saturation = value;
                UpdateRGBFromHSV();
                _isUpdatingValues = false;
            }
            OnPropertyChanged(nameof(Saturation));
        }
    }

    /// <summary>
    /// Command to select a color from the palette.
    /// </summary>
    public ICommand SelectColorCommand { get; private set; }

    /// <summary>
    /// Gets or sets the selected color.
    /// </summary>
    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set
        {
            SetValue(SelectedColorProperty, value);
        }
    }

    /// <summary>
    /// Gets the selected color as a brush.
    /// </summary>
    public SolidColorBrush SelectedColorBrush => new SolidColorBrush(SelectedColor);

    /// <summary>
    /// Gets or sets whether to show the alpha channel controls.
    /// </summary>
    public bool ShowAlphaChannel
    {
        get => (bool)GetValue(ShowAlphaChannelProperty);
        set => SetValue(ShowAlphaChannelProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show recent colors.
    /// </summary>
    public bool ShowRecentColors
    {
        get => (bool)GetValue(ShowRecentColorsProperty);
        set => SetValue(ShowRecentColorsProperty, value);
    }

    /// <summary>
    /// Gets a collection of standard colors.
    /// </summary>
    public ObservableCollection<SolidColorBrush> StandardColors => _standardColors;

    /// <summary>
    /// Gets or sets the value component of the color (0-100).
    /// </summary>
    public double Value
    {
        get => Math.Round(_value, 10);
        set
        {
            if (_value != value && !_isUpdatingValues)
            {
                _isUpdatingValues = true;
                _value = value;
                UpdateRGBFromHSV();
                _isUpdatingValues = false;
            }
            OnPropertyChanged(nameof(Value));
        }
    }

    /// <summary>
    /// Called when the template is applied.
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Get template parts
        _colorButton = GetTemplateChild("PART_ColorButton") as Button;
        _colorPickerPopup = GetTemplateChild("PART_ColorPickerPopup") as Popup;
        _okButton = GetTemplateChild("PART_OKButton") as Button;
        _cancelButton = GetTemplateChild("PART_CancelButton") as Button;
        _saturationValueCanvas = GetTemplateChild("PART_SaturationValueCanvas") as SaturationValueCanvas;
        _hueSlider = GetTemplateChild("PART_HueSlider") as HueSlider;

        // Debug: Check if template parts were found
        if (_colorButton == null) throw new InvalidOperationException("PART_ColorButton not found in template");
        if (_colorPickerPopup == null) throw new InvalidOperationException("PART_ColorPickerPopup not found in template");
        if (_okButton == null) throw new InvalidOperationException("PART_OKButton not found in template");
        if (_cancelButton == null) throw new InvalidOperationException("PART_CancelButton not found in template");
        if (_saturationValueCanvas == null) throw new InvalidOperationException("PART_SaturationValueCanvas not found in template");
        if (_hueSlider == null) throw new InvalidOperationException("PART_HueSlider not found in template");

        // Wire up event handlers
        if (_colorButton != null)
        {
            _colorButton.Click += ColorButton_Click;
        }

        if (_okButton != null)
        {
            _okButton.Click += OKButton_Click;
        }

        if (_cancelButton != null)
        {
            _cancelButton.Click += CancelButton_Click;
        }
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void AddToRecentColors(SolidColorBrush brush)
    {
        // Don't add if already in recent colors
        foreach (var recentBrush in _recentColors)
        {
            if (recentBrush.Color.Equals(brush.Color))
            {
                return;
            }
        }

        // Add to the beginning
        _recentColors.Insert(0, brush);

        // Limit to 22 recent colors
        while (_recentColors.Count > 22)
        {
            _recentColors.RemoveAt(_recentColors.Count - 1);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        // Restore the original color
        SelectedColor = _tempColor;

        // Close popup without applying changes
        if (_colorPickerPopup != null)
        {
            _colorPickerPopup.IsOpen = false;
        }
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_colorPickerPopup != null)
        {
            // Show or hide the popup
            _colorPickerPopup.IsOpen = !_colorPickerPopup.IsOpen;

            // When opening the popup, store the current color
            if (_colorPickerPopup.IsOpen)
            {
                _tempColor = SelectedColor;
            }
        }
    }

    /// <summary>
    /// Converts RGB color to HSV components.
    /// </summary>
    private static void ColorToHSV(Color color, out double hue, out double saturation, out double value)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        // Hue
        if (delta == 0)
        {
            hue = 0; // Undefined, use 0
        }
        else if (max == r)
        {
            hue = ((g - b) / delta) % 6;
        }
        else if (max == g)
        {
            hue = ((b - r) / delta) + 2;
        }
        else // max == b
        {
            hue = ((r - g) / delta) + 4;
        }

        hue *= 60;
        if (hue < 0)
            hue += 360;

        // Saturation
        saturation = (max == 0) ? 0 : (delta / max) * 100;

        // Value
        value = max * 100;
    }

    /// <summary>
    /// Converts HSV components to RGB color.
    /// </summary>
    private static Color HSVToColor(double hue, double saturation, double value, byte alpha = 255)
    {
        double h = hue;
        double s = saturation / 100.0;
        double v = value / 100.0;

        if (s <= 0.0)
        {
            byte bv = (byte)(v * 255);
            return Color.FromArgb(alpha, bv, bv, bv);
        }

        double hh = h / 60.0;
        int i = (int)Math.Floor(hh);
        double ff = hh - i;
        double p = v * (1.0 - s);
        double q = v * (1.0 - (s * ff));
        double t = v * (1.0 - (s * (1.0 - ff)));

        double r, g, b;
        switch (i)
        {
            case 0:
                r = v; g = t; b = p;
                break;
            case 1:
                r = q; g = v; b = p;
                break;
            case 2:
                r = p; g = v; b = t;
                break;
            case 3:
                r = p; g = q; b = v;
                break;
            case 4:
                r = t; g = p; b = v;
                break;
            default:
                r = v; g = p; b = q;
                break;
        }

        byte red = (byte)(r * 255);
        byte green = (byte)(g * 255);
        byte blue = (byte)(b * 255);

        return Color.FromArgb(alpha, red, green, blue);
    }

    private void InitializeAvailableColors()
    {
        // Add all colors from System.Windows.Media.Colors except Transparent
        _availableColors.Clear();
        var properties = typeof(Colors).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        foreach (var property in properties)
        {
            var value = property.GetValue(null);
            if (value is Color color && color != Colors.Transparent)
            {
                _availableColors.Add(new SolidColorBrush(color));
            }
        }
    }

    private void InitializeStandardColors()
    {
        // Only add 10 fixed standard colors
        var standardColors = new List<Color>
        {
            Colors.Transparent,
            Colors.White,
            Colors.Gray,
            Colors.Black,
            Colors.Red,
            Colors.Green,
            Colors.Blue,
            Colors.Yellow,
            Colors.Orange,
            Colors.Purple
        };
        _standardColors.Clear();
        foreach (var color in standardColors)
        {
            _standardColors.Add(new SolidColorBrush(color));
        }
    }

    private void OKButton_Click(object sender, RoutedEventArgs e)
    {
        // Apply the selected color and close popup
        AddToRecentColors(SelectedColorBrush);

        // Force binding to update by first setting a slightly different color,
        // then setting the actual color we want. This ensures the change is detected
        // in the binding chain even if equality comparisons are used.
        Color currentColor = SelectedColor;

        // Step 1: Make a small change to one component to ensure the color is seen as different
        byte modifiedRed = (byte)Math.Max(0, Math.Min(255, currentColor.R + (currentColor.R < 128 ? 1 : -1)));
        SelectedColor = Color.FromArgb(
            currentColor.A,
            modifiedRed,
            currentColor.G,
            currentColor.B);

        // Step 2: Now set it back to the actual desired color
        SelectedColor = Color.FromArgb(
            currentColor.A,
            currentColor.R,
            currentColor.G,
            currentColor.B);

        if (_colorPickerPopup != null)
        {
            _colorPickerPopup.IsOpen = false;
        }
    }

    private void OnSelectColor(SolidColorBrush brush)
    {
        if (brush != null)
        {
            SelectedColor = brush.Color;
        }
    }

    /// <summary>
    /// Called when a dependency property changes.
    /// </summary>
    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPicker colorPicker)
        {
            colorPicker.OnPropertyChanged(nameof(SelectedColor));
            colorPicker.OnPropertyChanged(nameof(SelectedColorBrush));
            colorPicker.UpdateColorComponents((Color)e.NewValue);
            // Only add to recent colors if not updating from RGB slider
            if (!colorPicker._isUpdatingValues)
            {
                colorPicker.AddToRecentColors(new SolidColorBrush((Color)e.NewValue));
            }
        }
    }

    /// <summary>
    /// Called when the ShowAlphaChannel property changes.
    /// </summary>
    private static void OnShowAlphaChannelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPicker colorPicker)
        {
            colorPicker.OnPropertyChanged(nameof(ShowAlphaChannel));
        }
    }

    private void UpdateColorComponents(Color color)
    {
        if (!_isUpdatingValues)
        {
            _isUpdatingValues = true;
            OnPropertyChanged(nameof(Red));
            OnPropertyChanged(nameof(Green));
            OnPropertyChanged(nameof(Blue));
            OnPropertyChanged(nameof(Alpha));
            OnPropertyChanged(nameof(HexColor));
            UpdateHSVFromRGB();
            _isUpdatingValues = false;
        }
    }

    private void UpdateHSVFromRGB()
    {
        ColorToHSV(SelectedColor, out double h, out double s, out double v);
        _hue = h;
        _saturation = s;
        _value = v;
        OnPropertyChanged(nameof(Hue));
        OnPropertyChanged(nameof(Saturation));
        OnPropertyChanged(nameof(Value));
    }

    private void UpdateRGBFromHSV()
    {
        Color color = HSVToColor(_hue, _saturation, _value, SelectedColor.A);
        SelectedColor = color;
    }
}