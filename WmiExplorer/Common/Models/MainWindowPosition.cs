using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;

namespace WmiExplorer.Common.Models;

/// <summary>
/// Represents the position and size of the main window
/// </summary>
public partial class MainWindowPosition : ObservableObject
{
    // Constants
    public const double DEFAULT_COLUMN_WIDTH = 300;
    public const double DEFAULT_HEIGHT = 900;
    public const double DEFAULT_LEFT = 100;
    public const double DEFAULT_TOP = 100;
    public const double DEFAULT_WIDTH = 1440;
    public const double FLUCTUATION_THRESHOLD = 1;
    public const double MIN_COLUMN_WIDTH = 30;

    // Private fields with ObservableProperty attributes
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClassesColumnGridLength))]
    private double _classesColumnWidth = DEFAULT_COLUMN_WIDTH;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContentColumnStarGridLength))]
    private double _contentColumnStarWidth = 2.0;

    [ObservableProperty]
    private double _height = DEFAULT_HEIGHT;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClassesColumnGridLength))]
    private bool _isClassesExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NamespaceColumnGridLength))]
    private bool _isNamespacesExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PropertyGridColumnStarGridLength))]
    private bool _isPropertyGridExpanded = true;

    [ObservableProperty]
    private bool _isWindowMaximized = false;

    [ObservableProperty]
    private double _left = DEFAULT_LEFT;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NamespaceColumnGridLength))]
    private double _namespaceColumnWidth = DEFAULT_COLUMN_WIDTH;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PropertyGridColumnStarGridLength))]
    private double _propertyGridColumnStarWidth = 1.0;

    [ObservableProperty]
    private double _top = DEFAULT_TOP;

    [ObservableProperty]
    private double _width = DEFAULT_WIDTH;

    /// <summary>
    /// Gets a UI-friendly GridLength for the classes column width based on expander state
    /// </summary>
    [JsonIgnore]
    public GridLength ClassesColumnGridLength
    {
        get => GetColumnWidth(IsClassesExpanded, ClassesColumnWidth);
        set
        {
            if (value.GridUnitType == GridUnitType.Pixel && value.Value > MIN_COLUMN_WIDTH &&
                IsClassesExpanded &&
                Math.Abs(value.Value - ClassesColumnWidth) > FLUCTUATION_THRESHOLD) // Avoid minor fluctuations
            {
                ClassesColumnWidth = value.Value;
            }
        }
    }

    /// <summary>
    /// Gets a UI-friendly star-based GridLength for the content column
    /// </summary>
    [JsonIgnore]
    public GridLength ContentColumnStarGridLength
    {
        get => GetColumnWidth(true, ContentColumnStarWidth, GridUnitType.Star);
        set
        {
            if (value.GridUnitType == GridUnitType.Star && value.Value > 0)
            {
                ContentColumnStarWidth = value.Value;
            }
        }
    }

    /// <summary>
    /// Gets a UI-friendly GridLength for the namespaces column width based on expander state
    /// </summary>
    [JsonIgnore]
    public GridLength NamespaceColumnGridLength
    {
        get => GetColumnWidth(IsNamespacesExpanded, NamespaceColumnWidth);
        set
        {
            if (value.GridUnitType == GridUnitType.Pixel && value.Value > MIN_COLUMN_WIDTH &&
                IsNamespacesExpanded &&
                Math.Abs(value.Value - NamespaceColumnWidth) > FLUCTUATION_THRESHOLD) // Avoid minor fluctuations
            {
                NamespaceColumnWidth = value.Value;
            }
        }
    }

    /// <summary>
    /// Gets a UI-friendly GridLength for the property grid column
    /// </summary>
    [JsonIgnore]
    public GridLength PropertyGridColumnStarGridLength
    {
        get => GetColumnWidth(IsPropertyGridExpanded, PropertyGridColumnStarWidth, GridUnitType.Star);
        set
        {
            if (value.GridUnitType == GridUnitType.Star && value.Value > 0 &&
                IsPropertyGridExpanded)
            {
                PropertyGridColumnStarWidth = value.Value;
            }
        }
    }

    /// <summary>
    /// Updates the position with new values (only window geometry)
    /// </summary>
    public void UpdatePosition(
        double left,
        double top,
        double width,
        double height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    // Returns a GridLength for a column, using Pixel, Star or Auto based on unitType
    private GridLength GetColumnWidth(bool isExpanded, double savedWidth, GridUnitType unitType = GridUnitType.Pixel)
    {
        if (isExpanded)
        {
            // Use saved width (or safe default) when expanded
            if (unitType == GridUnitType.Star)
            {
                // For star units, just use the value directly (must be > 0)
                return new GridLength(savedWidth > 0 ? savedWidth : 1.0, GridUnitType.Star);
            }
            else
            {
                // For pixel units, apply minimum width
                double width = savedWidth >= MIN_COLUMN_WIDTH ? savedWidth : DEFAULT_COLUMN_WIDTH;
                return new GridLength(width, unitType);
            }
        }
        else
        {
            // When collapsed, return Auto width
            // The expander header width is controlled by the fixed Border width in XAML
            return new GridLength(0, GridUnitType.Auto);
        }
    }

    /// <summary>
    /// Partial method called when IsPropertyGridExpanded changes
    /// Handles special logic for property grid expansion state
    /// </summary>
    partial void OnIsPropertyGridExpandedChanged(bool value)
    {
        // When toggling expander state, we need to ensure the correct column widths
        if (value == false)
        {
            // When collapsing, we store the current star value for later
            // No additional action needed as PropertyGridColumnStarGridLength handles this
        }
        else
        {
            // When expanding, we ensure we have a reasonable star value
            if (PropertyGridColumnStarWidth <= 0)
            {
                PropertyGridColumnStarWidth = 1.0;
            }
        }
    }
}