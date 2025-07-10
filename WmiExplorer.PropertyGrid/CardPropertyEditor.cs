using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WmiExplorer.PropertyGrid.Abstractions;
using WmiExplorer.PropertyGrid.Converters;

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

        // Get the editor content using the registry system and wrap it in a simple card layout
        var editorContent = GetEditorContent(newPropertyItem);
        Content = CreateCardContent(editorContent, newPropertyItem);
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

        // Name column - bind width to PropertyGrid's NameColumnWidth
        var nameColumn = new ColumnDefinition();
        nameColumn.SetBinding(ColumnDefinition.WidthProperty,
            new Binding("NameColumnWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(PropertyGrid), 1),
                Converter = new DoubleToGridLengthConverter()
            });
        grid.ColumnDefinitions.Add(nameColumn);

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
            Text = $"[{PropertyEditorUtils.GetFriendlyTypeName(propertyItem.PropertyType)}]",
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

            // Calculate width to subtract: column0 width (dynamic) + fe margin (8) + card padding (8*2) + border/spacing buffer (8)
            // We'll create a multi-binding to subtract the name column width + fixed spacing
            var multiBinding = new MultiBinding
            {
                Converter = new WidthCalculationConverter()
            };

            multiBinding.Bindings.Add(new Binding("ActualWidth") { Source = grid });
            multiBinding.Bindings.Add(new Binding("NameColumnWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(PropertyGrid), 1)
            });

            fe.SetBinding(FrameworkElement.MaxWidthProperty, multiBinding);
        }

        Grid.SetColumn(content, 1);
        grid.Children.Add(content);

        cardBorder.Child = grid;
        return cardBorder;
    }

    /// <summary>
    /// Gets the appropriate editor content for the property item using the registry system
    /// </summary>
    private UIElement GetEditorContent(PropertyHierarchyItem propertyItem)
    {
        // First, try to find a specialized editor using the same logic as base PropertyEditor
        var specializedEditor = PropertyEditorRegistry.Instance.GetEditor(propertyItem);
        if (specializedEditor != null && specializedEditor != this)
        {
            return specializedEditor.CreateEditor(propertyItem);
        }
        else
        {
            // Fall back to the core editor
            return CreateCoreEditor(propertyItem);
        }
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

/// <summary>
/// Converter for calculating the maximum width of the editor column
/// </summary>
public class WidthCalculationConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (values.Length >= 2 &&
            values[0] is double gridWidth &&
            values[1] is double nameColumnWidth)
        {
            // Calculate width to subtract: nameColumnWidth + fe margin (8) + card padding (8*2) + border/spacing buffer (8)
            double widthToSubtract = nameColumnWidth + 8 + 16 + 8;
            double maxWidth = gridWidth - widthToSubtract;

            return Math.Max(maxWidth, 50); // Ensure minimum width of 50
        }

        return 200.0; // Fallback width
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}