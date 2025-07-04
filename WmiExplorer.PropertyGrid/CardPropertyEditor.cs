using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// A specialized property editor that provides a clean card-style layout.
/// Inherits from PropertyEditor to reuse core editing functionality.
/// </summary>
public class CardPropertyEditor : PropertyEditor
{
    static CardPropertyEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CardPropertyEditor),
            new FrameworkPropertyMetadata(typeof(CardPropertyEditor)));
    }

    protected override void OnPropertyItemChanged(PropertyHierarchyItem? newPropertyItem)
    {
        if (newPropertyItem == null)
        {
            Content = null;
            return;
        }

        // Get the base editor content and wrap it in a simple card layout
        var baseContent = CreateCoreEditor(newPropertyItem);
        Content = CreateCardContent(baseContent, newPropertyItem);
    }

    /// <summary>
    /// Creates the card content with a simple two-column layout
    /// </summary>
    private UIElement CreateCardContent(UIElement content, PropertyHierarchyItem propertyItem)
    {
        // Main card border
        var cardBorder = new Border
        {
            Margin = new Thickness(-12, 2, 4, 2), // Negative left margin for TreeView alignment
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(6, 1, 1, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 30
        };

        // Set up styling with fallbacks
        SetupCardStyling(cardBorder, propertyItem);

        // Two-column grid layout
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // Fixed name column
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Stretching editor column

        // Property name and type column
        var namePanel = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };

        namePanel.Children.Add(new TextBlock
        {
            Text = propertyItem.DisplayName ?? propertyItem.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11
        });

        namePanel.Children.Add(new TextBlock
        {
            Text = $"[{GetFriendlyTypeName(propertyItem.PropertyType)}]",
            FontStyle = FontStyles.Italic,
            FontSize = 10,
            Opacity = 0.6
        });

        // Create converter instance for width constraints (used for column 1)
        var maxWidthConverter = new MaxWidthConverter();

        Grid.SetColumn(namePanel, 0);
        grid.Children.Add(namePanel);

        // Editor content column
        if (content is FrameworkElement fe)
        {
            fe.Margin = new Thickness(8, 0, 0, 0);
            fe.VerticalAlignment = VerticalAlignment.Center;
            fe.HorizontalAlignment = HorizontalAlignment.Stretch;

            // Calculate width to subtract: column0 width (150) + fe margin (8) + card padding (8*2) + border/spacing buffer (8)
            double widthToSubtract = 150 + 8 + 16 + 8; // = 182

            // Apply MaxWidthConverter to constrain width to card's width
            fe.SetBinding(FrameworkElement.MaxWidthProperty,
                new Binding("ActualWidth") { Source = grid, Converter = maxWidthConverter, ConverterParameter = widthToSubtract });
        }

        Grid.SetColumn(content, 1);
        grid.Children.Add(content);

        cardBorder.Child = grid;
        return cardBorder;
    }

    /// <summary>
    /// Sets up card styling with selection highlighting
    /// </summary>
    private void SetupCardStyling(Border cardBorder, PropertyHierarchyItem propertyItem)
    {
        // Try to use theme resources, fallback to simple colors
        try
        {
            cardBorder.SetResourceReference(Border.BackgroundProperty, "SecondaryBackgroundBrush");
        }
        catch
        {
            cardBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(40, 100, 150, 200));
        }

        // Selection highlighting
        var selectionBinding = new Binding("IsSelected") { Source = propertyItem };

        var borderBrushConverter = new SelectionBorderBrushConverter();
        cardBorder.SetBinding(Border.BorderBrushProperty,
            new Binding("IsSelected") { Source = propertyItem, Converter = borderBrushConverter });
    }
}

/// <summary>
/// Converter for border brush based on selection state
/// </summary>
public class SelectionBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            try
            {
                return Application.Current.FindResource("PropertyGridAccentBrush");
            }
            catch
            {
                return new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0, 120, 215));
            }
        }

        try
        {
            return Application.Current.FindResource("BorderBrush");
        }
        catch
        {
            return System.Windows.Media.Brushes.LightGray;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}