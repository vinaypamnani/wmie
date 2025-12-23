using System.Windows.Media;

namespace WmiExplorer.Presentation.Controls.ColorPicker;

/// <summary>
/// Utility class for color conversion between RGB and HSV.
/// </summary>
public static class ColorUtils
{
    /// <summary>
    /// Converts RGB color to HSV components.
    /// </summary>
    public static void ColorToHSV(Color color, out double hue, out double saturation, out double value)
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
    public static Color HSVToColor(double hue, double saturation, double value, byte alpha = 255)
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
}