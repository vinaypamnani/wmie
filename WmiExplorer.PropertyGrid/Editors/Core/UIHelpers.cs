using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WmiExplorer.PropertyGrid.Editors.Converters;

namespace WmiExplorer.PropertyGrid.Editors.Core;

/// <summary>
/// UI helper utilities for property editors providing layout and styling functionality.
/// </summary>
public static class UIHelpers
{
    /// <summary>
    /// Applies MaxWidth constraint to any FrameworkElement based on parent container
    /// </summary>
    public static void ApplyMaxWidthConstraint(FrameworkElement element, FrameworkElement? parentContainer = null, double widthToSubtract = 20, bool forceApply = false)
    {
        // Check if element already has a MaxWidth binding (to avoid double-binding)
        if (!forceApply && BindingOperations.GetBinding(element, FrameworkElement.MaxWidthProperty) != null)
        {
            return; // Already has a MaxWidth binding, don't override
        }

        FrameworkElement? targetParent = parentContainer;
        if (targetParent == null)
        {
            // Walk up the visual tree to find the first ancestor with a finite ActualWidth
            DependencyObject? current = element;
            while (current != null)
            {
                current = VisualTreeHelper.GetParent(current);
                if (current is FrameworkElement fe && fe.ActualWidth > 0 && !double.IsInfinity(fe.ActualWidth))
                {
                    targetParent = fe;
                    break;
                }
            }
        }

        if (targetParent != null)
        {
            var maxWidthConverter = new MaxWidthConverter();
            element.SetBinding(FrameworkElement.MaxWidthProperty,
                new Binding("ActualWidth") { Source = targetParent, Converter = maxWidthConverter, ConverterParameter = widthToSubtract });
        }
        else
        {
            // Fallback: use RelativeSource to find a Grid ancestor
            element.SetBinding(FrameworkElement.MaxWidthProperty,
                new Binding("ActualWidth") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Grid), 1), Converter = new MaxWidthConverter(), ConverterParameter = widthToSubtract });
        }
    }

    /// <summary>
    /// Creates a standardized TextBox for property editing with consistent styling and behavior
    /// </summary>
    public static TextBox CreateStandardTextBox(string? initialText = null, string? placeholder = null, PropertyHierarchyItem? propertyItem = null, Thickness? margin = null, System.Func<string, object?, ValidationManager.ValidationResult>? customValidation = null)
    {
        var textBox = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = margin ?? EditorInfrastructure.CONTROL_MARGIN_STANDARD,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        PropertyEditorUtils.InitializeEditor(textBox, propertyItem!);

        // Apply MaxWidth constraint using the generic method
        ApplyMaxWidthConstraint(textBox);

        if (!string.IsNullOrEmpty(initialText))
        {
            textBox.Text = initialText;
        }

        textBox.Tag = placeholder ?? "Enter value";

        // Try to apply style if available
        try
        {
            if (Application.Current.TryFindResource("PropertyGridTextBoxWithPlaceholder") is Style style)
            {
                textBox.Style = style;
            }
        }
        catch
        {
            // Style not available, continue without it
        }

        // Attach selection behavior if property item provided
        EditorInfrastructure.AttachSelectOnFocus(textBox, propertyItem);

        // Attach validation behavior if property item provided
        if (propertyItem != null)
        {
            if (customValidation != null)
                ValidationManager.AddValidationBehavior(textBox, propertyItem, customValidation);
            else
                ValidationManager.AddValidationBehavior(textBox, propertyItem);
        }

        return textBox;
    }

    /// <summary>
    /// Creates a tip TextBlock with standard styling
    /// </summary>
    public static TextBlock CreateTipTextBlock(string tipText)
    {
        var tipTextBlock = new TextBlock
        {
            Text = $"💡 {tipText}",
            FontSize = 11,
            FontStyle = FontStyles.Italic,
            Opacity = 0.7,
            Margin = EditorInfrastructure.TIP_TEXT_MARGIN,
            TextWrapping = TextWrapping.Wrap
        };

        try
        {
            tipTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "PropertyGridForegroundBrush");
        }
        catch
        {
            tipTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
        }

        return tipTextBlock;
    }
}