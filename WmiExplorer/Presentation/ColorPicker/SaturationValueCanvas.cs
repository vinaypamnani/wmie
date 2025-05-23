using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WmiExplorer.Presentation.ColorPicker;

/// <summary>
/// A canvas that displays a 2D saturation-value selector for a given hue.
/// </summary>
public class SaturationValueCanvas : Control
{
    public static readonly DependencyProperty HueProperty =
        DependencyProperty.Register(
            nameof(Hue),
            typeof(double),
            typeof(SaturationValueCanvas),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHueChanged));

    public static readonly DependencyProperty SaturationProperty =
        DependencyProperty.Register(
            nameof(Saturation),
            typeof(double),
            typeof(SaturationValueCanvas),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSaturationValueChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(SaturationValueCanvas),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSaturationValueChanged));

    private WriteableBitmap? _bitmap;
    private bool _isMouseDown = false;

    public SaturationValueCanvas()
    {
        this.Background = Brushes.Transparent;
        this.Focusable = true;
        this.Cursor = Cursors.Cross;
        this.SizeChanged += SaturationValueCanvas_SizeChanged;

        // Register mouse events
        this.MouseDown += SaturationValueCanvas_MouseDown;
        this.MouseMove += SaturationValueCanvas_MouseMove;
        this.MouseUp += SaturationValueCanvas_MouseUp;
    }

    static SaturationValueCanvas()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SaturationValueCanvas), new FrameworkPropertyMetadata(typeof(SaturationValueCanvas)));
    }

    /// <summary>
    /// Gets or sets the current hue (0-360)
    /// </summary>
    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, Math.Clamp(value, 0, 360));
    }

    /// <summary>
    /// Gets or sets the current saturation (0-100)
    /// </summary>
    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, Math.Clamp(value, 0, 100));
    }

    /// <summary>
    /// Gets or sets the current value (0-100)
    /// </summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, 0, 100));
    }

    /// <summary>
    /// On render, draw the saturation-value bitmap and the selection cursor
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_bitmap == null && ActualWidth > 0 && ActualHeight > 0)
        {
            CreateSVBitmap();
        }

        if (_bitmap != null)
        {
            // Draw the saturation-value bitmap
            drawingContext.DrawImage(_bitmap, new Rect(0, 0, ActualWidth, ActualHeight));

            // Draw a border around the canvas
            drawingContext.DrawRectangle(
                null,
                new Pen(BorderBrush ?? Brushes.Gray, 1),
                new Rect(0, 0, ActualWidth, ActualHeight));

            // Draw the selection cursor
            double x = (Saturation / 100.0) * ActualWidth;
            double y = (1 - Value / 100.0) * ActualHeight;
            double cursorSize = 10;

            // Determine if we need a light or dark cursor based on the color brightness
            Color currentColor = ColorFromHSV(Hue, Saturation, Value);
            double brightness = (0.299 * currentColor.R + 0.587 * currentColor.G + 0.114 * currentColor.B) / 255;
            Brush cursorBrush = brightness > 0.5 ? Brushes.Black : Brushes.White;

            // Draw outer circle
            drawingContext.DrawEllipse(
                null,
                new Pen(cursorBrush, 2),
                new Point(x, y),
                cursorSize, cursorSize);

            // Draw inner circle
            drawingContext.DrawEllipse(
                null,
                new Pen(Brushes.Gray, 1),
                new Point(x, y),
                cursorSize - 3, cursorSize - 3);
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
    /// Creates a bitmap with the saturation-value grid for the current hue
    /// </summary>
    private void CreateSVBitmap()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        int width = Math.Max(1, (int)Math.Floor(ActualWidth));
        int height = Math.Max(1, (int)Math.Floor(ActualHeight));

        try
        {
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            int bytesPerPixel = (_bitmap.Format.BitsPerPixel + 7) / 8;
            int stride = width * bytesPerPixel;
            byte[] pixels = new byte[height * stride];

            // Draw the saturation-value grid
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double saturation = (double)x / width * 100.0;
                    double value = (1.0 - (double)y / height) * 100.0;
                    Color color = ColorFromHSV(Hue, saturation, value);

                    int index = y * stride + x * bytesPerPixel;
                    pixels[index] = color.B;     // Blue
                    pixels[index + 1] = color.G; // Green
                    pixels[index + 2] = color.R; // Red
                    pixels[index + 3] = 255;     // Alpha
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        }
        catch (Exception)
        {
            // If bitmap creation fails, clear the bitmap and return
            _bitmap = null;
        }
    }

    private static void OnHueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SaturationValueCanvas canvas)
        {
            canvas.CreateSVBitmap();
            canvas.InvalidateVisual();
        }
    }

    private static void OnSaturationValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SaturationValueCanvas canvas)
        {
            canvas.InvalidateVisual();
        }
    }

    private void SaturationValueCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isMouseDown = true;
            CaptureMouse();
            UpdateSVFromMousePosition(e.GetPosition(this));
        }
    }

    private void SaturationValueCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isMouseDown && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateSVFromMousePosition(e.GetPosition(this));
        }
    }

    private void SaturationValueCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMouseDown)
        {
            _isMouseDown = false;
            ReleaseMouseCapture();
        }
    }

    private void SaturationValueCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Recreate the bitmap when size changes
        CreateSVBitmap();
        InvalidateVisual();
    }

    /// <summary>
    /// Updates the saturation and value based on the mouse position
    /// </summary>
    private void UpdateSVFromMousePosition(Point position)
    {
        double saturationPercentage = Math.Clamp(position.X / ActualWidth, 0, 1);
        double valuePercentage = Math.Clamp(1 - (position.Y / ActualHeight), 0, 1);

        Saturation = saturationPercentage * 100;
        Value = valuePercentage * 100;
    }
}