using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WmiExplorer.Presentation.ColorPicker;

/// <summary>
/// A custom slider that displays the hue spectrum and allows selection of a hue value.
/// </summary>
public class HueSlider : Control
{
    public static readonly DependencyProperty HueProperty =
        DependencyProperty.Register(
            nameof(Hue),
            typeof(double),
            typeof(HueSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHueChanged));

    private WriteableBitmap? _bitmap;
    private bool _isMouseDown = false;

    public HueSlider()
    {
        // Create the default template
        // We'll render the slider ourselves in OnRender
        this.Background = Brushes.Transparent;
        this.Focusable = true;
        this.Cursor = Cursors.Hand;
        this.SizeChanged += HueSlider_SizeChanged;

        // Register mouse events
        this.MouseDown += HueSlider_MouseDown;
        this.MouseMove += HueSlider_MouseMove;
        this.MouseUp += HueSlider_MouseUp;
    }

    static HueSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HueSlider), new FrameworkPropertyMetadata(typeof(HueSlider)));
    }

    /// <summary>
    /// Gets or sets the current hue value (0-360)
    /// </summary>
    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, Math.Clamp(value, 0, 360));
    }

    /// <summary>
    /// On render, draw the hue spectrum and the thumb indicator
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_bitmap == null && ActualWidth > 0 && ActualHeight > 0)
        {
            CreateHueBitmap();
        }

        if (_bitmap != null)
        {
            // Draw the hue gradient bitmap
            drawingContext.DrawImage(_bitmap, new Rect(0, 0, ActualWidth, ActualHeight));

            // Draw a border around the slider
            drawingContext.DrawRectangle(
                null,
                new Pen(BorderBrush ?? Brushes.Gray, 1),
                new Rect(0, 0, ActualWidth, ActualHeight));

            // Draw the thumb indicator
            double thumbPosition = (Hue / 360.0) * ActualWidth;
            double thumbWidth = 4;
            double thumbHeight = ActualHeight + 6;

            Brush thumbBrush = new SolidColorBrush(Colors.White);
            Pen thumbOutlinePen = new Pen(new SolidColorBrush(Colors.Black), 1);

            drawingContext.DrawRectangle(
                thumbBrush,
                thumbOutlinePen,
                new Rect(thumbPosition - thumbWidth / 2, -3, thumbWidth, thumbHeight));
        }
    }

    /// <summary>
    /// Converts HSV to RGB Color
    /// </summary>
    private static Color ColorFromHSV(double hue, double saturation, double value)
    {
        double h = hue;
        double s = saturation / 100.0;
        double v = value / 100.0;

        double r, g, b;
        if (s == 0)
        {
            r = g = b = v;
        }
        else
        {
            int i = (int)Math.Floor(h / 60) % 6;
            double f = h / 60 - Math.Floor(h / 60);
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
        }

        return Color.FromRgb(
            (byte)(r * 255),
            (byte)(g * 255),
            (byte)(b * 255));
    }

    /// <summary>
    /// Creates a bitmap with the hue spectrum
    /// </summary>
    private void CreateHueBitmap()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;

        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        int bytesPerPixel = (_bitmap.Format.BitsPerPixel + 7) / 8;
        int stride = width * bytesPerPixel;
        byte[] pixels = new byte[height * stride];

        // Draw the hue gradient
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double hue = (double)x / width * 360.0;
                Color color = ColorFromHSV(hue, 100, 100);

                int index = y * stride + x * bytesPerPixel;
                pixels[index] = color.B;     // Blue
                pixels[index + 1] = color.G; // Green
                pixels[index + 2] = color.R; // Red
                pixels[index + 3] = 255;     // Alpha
            }
        }

        _bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
    }

    private void HueSlider_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isMouseDown = true;
            CaptureMouse();
            UpdateHueFromMousePosition(e.GetPosition(this));
        }
    }

    private void HueSlider_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isMouseDown && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateHueFromMousePosition(e.GetPosition(this));
        }
    }

    private void HueSlider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMouseDown)
        {
            _isMouseDown = false;
            ReleaseMouseCapture();
        }
    }

    private void HueSlider_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Recreate the bitmap when size changes
        CreateHueBitmap();
        InvalidateVisual();
    }

    private static void OnHueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HueSlider slider)
        {
            slider.InvalidateVisual();
        }
    }

    /// <summary>
    /// Updates the hue value based on the mouse position
    /// </summary>
    private void UpdateHueFromMousePosition(Point position)
    {
        double widthPercentage = Math.Clamp(position.X / ActualWidth, 0, 1);
        Hue = widthPercentage * 360;
    }
}