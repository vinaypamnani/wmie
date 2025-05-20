using System.Windows;
using System.Windows.Controls;

namespace WmiExplorer.PropertyGrid
{
    /// <summary>
    /// Custom panel for PropertyGrid tree items to create a two-column grid-like layout
    /// with proper indentation for hierarchical items.
    /// </summary>
    public class PropertyTreePanel : VirtualizingStackPanel
    {
        /// <summary>
        /// Gets or sets the indentation per level.
        /// </summary>
        public static readonly DependencyProperty IndentationProperty =
            DependencyProperty.RegisterAttached(
                "Indentation",
                typeof(double),
                typeof(PropertyTreePanel),
                new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsArrange |
                                                  FrameworkPropertyMetadataOptions.AffectsMeasure |
                                                  FrameworkPropertyMetadataOptions.Inherits));

        /// <summary>
        /// Gets or sets the level of a tree item.
        /// </summary>
        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.RegisterAttached(
                "Level",
                typeof(int),
                typeof(PropertyTreePanel),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsArrange |
                                                FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// Gets or sets the width of the name column.
        /// </summary>
        public static readonly DependencyProperty NameColumnWidthProperty =
            DependencyProperty.RegisterAttached(
                "NameColumnWidth",
                typeof(double),
                typeof(PropertyTreePanel),
                new FrameworkPropertyMetadata(150.0, FrameworkPropertyMetadataOptions.AffectsArrange |
                                                     FrameworkPropertyMetadataOptions.AffectsMeasure |
                                                     FrameworkPropertyMetadataOptions.Inherits));

        public static double GetIndentation(DependencyObject obj)
        {
            return (double)obj.GetValue(IndentationProperty);
        }

        public static int GetLevel(DependencyObject obj)
        {
            return (int)obj.GetValue(LevelProperty);
        }

        public static double GetNameColumnWidth(DependencyObject obj)
        {
            return (double)obj.GetValue(NameColumnWidthProperty);
        }

        public static void SetIndentation(DependencyObject obj, double value)
        {
            obj.SetValue(IndentationProperty, value);
        }

        public static void SetLevel(DependencyObject obj, int value)
        {
            obj.SetValue(LevelProperty, value);
        }

        public static void SetNameColumnWidth(DependencyObject obj, double value)
        {
            obj.SetValue(NameColumnWidthProperty, value);
        }
    }
}