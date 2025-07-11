using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WmiExplorer.PropertyGrid.Abstractions;
using WmiExplorer.PropertyGrid.Converters;
using WmiExplorer.PropertyGrid.Editors.Converters;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.Specialized;

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

        // Three-column grid layout: [name][icon][editor]
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

        // Icon column (fixed width, e.g., 24)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Stretching editor column

        // Property name and type column
        var namePanel = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };

        // Create horizontal panel for property name with key icon
        var nameWithIconPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // Add key icon if this is a key property (BEFORE the property name)
        if (propertyItem.IsKey)
        {
            var keyIcon = new ContentControl
            {
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Key Property"
            };

            // Set the ContentTemplate to use the KeyIconTemplate
            try
            {
                keyIcon.ContentTemplate = (DataTemplate)Application.Current.FindResource("KeyIconTemplate");
                keyIcon.Foreground = (Brush)Application.Current.FindResource("PropertyGridKeyHighlightBrush");
            }
            catch
            {
                // Fallback to simple text icon if KeyIconTemplate is not available
                keyIcon.Content = new TextBlock
                {
                    Text = "🔑",
                    FontSize = 8,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            nameWithIconPanel.Children.Add(keyIcon);
        }

        nameWithIconPanel.Children.Add(new TextBlock
        {
            Text = propertyItem.DisplayName ?? propertyItem.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        });

        namePanel.Children.Add(nameWithIconPanel);

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

        // --- Validation Icon ---
        // Try to find the TextBox inside the editor content
        TextBox? editorTextBox = null;
        if (content is TextBox tb)
            editorTextBox = tb;

        else if (content is Panel panel)
        {
            // Look for first TextBox child
            foreach (var child in panel.Children)
            {
                if (child is TextBox t)
                {
                    editorTextBox = t;
                    break;
                }
            }
        }
        else if (content is Grid gridContent)
        {
            foreach (var child in gridContent.Children)
            {
                if (child is TextBox t)
                {
                    editorTextBox = t;
                    break;
                }
            }
        }
        // If not found, icon will not be shown

        var iconPanel = new Grid { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (editorTextBox != null)
        {
            var iconText = new System.Windows.Controls.TextBlock
            {
                Style = (Style)Application.Current.FindResource("PropertyGridIconStyle"),
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0) // Add right margin
            };

            // Bind icon glyph and color using the converter
            var iconBinding = new Binding
            {
                Path = new PropertyPath("(0)", ValidationManager.ValidationStateProperty),
                Source = editorTextBox,
                Converter = new Converters.ValidationStateToIconConverter()
            };
            iconText.SetBinding(System.Windows.Controls.TextBlock.VisibilityProperty, new Binding
            {
                Path = new PropertyPath("(0)", ValidationManager.ValidationStateProperty),
                Source = editorTextBox,
                Converter = new Converters.ValidationStateToVisibilityConverter() // You may need to add this
            });
            iconText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath("(0)", ValidationManager.ValidationStateProperty),
                Source = editorTextBox,
                Converter = new Converters.ValidationStateToGlyphConverter() // You may need to add this
            });
            iconText.SetBinding(System.Windows.Controls.TextBlock.ForegroundProperty, new Binding
            {
                Path = new PropertyPath("(0)", ValidationManager.ValidationStateProperty),
                Source = editorTextBox,
                Converter = new Converters.ValidationStateToBrushConverter() // You may need to add this
            });
            // Tooltip: bind to the TextBox's ToolTip
            iconText.SetBinding(System.Windows.Controls.TextBlock.ToolTipProperty, new Binding
            {
                Source = editorTextBox,
                Path = new PropertyPath("ToolTip")
            });
            iconPanel.Children.Add(iconText);
        }
        Grid.SetColumn(iconPanel, 1);
        grid.Children.Add(iconPanel);

        // Editor content column
        if (content is FrameworkElement fe)
        {
            fe.Margin = new Thickness(8, 0, 0, 0);
            fe.VerticalAlignment = VerticalAlignment.Center;
            fe.HorizontalAlignment = HorizontalAlignment.Stretch;

            // Calculate width to subtract: name column width + icon column width + fe margin (8) + card padding (4*2) + border/spacing buffer (8)
            var multiBinding = new MultiBinding
            {
                Converter = new WidthCalculationConverter()
            };
            multiBinding.Bindings.Add(new Binding("ActualWidth") { Source = grid });
            multiBinding.Bindings.Add(new Binding("NameColumnWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(PropertyGrid), 1)
            });
            // Add icon column width (fixed 24)
            multiBinding.Bindings.Add(new Binding { Source = 24.0 });
            fe.SetBinding(FrameworkElement.MaxWidthProperty, multiBinding);
        }
        Grid.SetColumn(content, 2);
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