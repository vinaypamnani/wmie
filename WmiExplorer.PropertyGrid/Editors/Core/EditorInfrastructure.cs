using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid.Editors.Core;

/// <summary>
/// Core infrastructure for property editors providing common functionality,
/// binding creation, and standard configuration.
/// </summary>
public static class EditorInfrastructure
{
    public static readonly Thickness CHECKBOX_HEX_MARGIN = new(6, 0, 6, 0);
    public static readonly Thickness CONTROL_MARGIN_STANDARD = new(0, 2, 2, 2);
    public static readonly Thickness TIP_TEXT_MARGIN = new(4, 4, 4, 4);

    /// <summary>
    /// Applies standard configuration to a FrameworkElement for property editing
    /// </summary>
    public static void ApplyStandardEditorConfiguration(FrameworkElement element, PropertyHierarchyItem propertyItem, bool applyMaxWidth = true)
    {
        element.VerticalAlignment = VerticalAlignment.Center;
        element.HorizontalAlignment = HorizontalAlignment.Stretch;
        element.Margin = CONTROL_MARGIN_STANDARD;

        if (applyMaxWidth)
        {
            UIHelpers.ApplyMaxWidthConstraint(element);
        }

        if (element is Control control)
        {
            AttachSelectOnFocus(control, propertyItem);
        }
    }

    /// <summary>
    /// Helper to attach focus event for selection
    /// </summary>
    public static void AttachSelectOnFocus(Control control, PropertyHierarchyItem? propertyItem)
    {
        if (propertyItem != null && control != null)
        {
            control.GotFocus += (s, e) => propertyItem.IsSelected = true;
        }
    }

    /// <summary>
    /// Creates a standard Grid layout with main content and an action panel (which can be a single control or a container)
    /// </summary>
    public static Grid CreateGridWithActionPanel(UIElement mainContent, UIElement actionPanel, double actionPanelWidth = 80)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(mainContent, 0);
        grid.Children.Add(mainContent);

        if (actionPanel != null)
        {
            if (actionPanel is FrameworkElement fe)
            {
                fe.Margin = new Thickness(8, 0, 0, 0);
                fe.VerticalAlignment = VerticalAlignment.Center;
                // Only set width if it's a single control and width is not set
                if (fe.Width.Equals(double.NaN) && actionPanelWidth > 0 && !(actionPanel is Panel))
                {
                    fe.Width = actionPanelWidth;
                }
            }

            Grid.SetColumn(actionPanel, 1);
            grid.Children.Add(actionPanel);
        }

        // Apply MaxWidth constraint to main content accounting for action panel width
        if (mainContent is FrameworkElement mainElement)
        {
            UIHelpers.ApplyMaxWidthConstraint(mainElement, grid, actionPanelWidth + 16); // Account for panel width + margins
        }

        return grid;
    }

    /// <summary>
    /// Creates a standard two-way binding for property values
    /// </summary>
    public static Binding CreateStandardPropertyBinding(PropertyHierarchyItem propertyItem, UpdateSourceTrigger trigger = UpdateSourceTrigger.PropertyChanged)
    {
        return new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = trigger,
            ValidatesOnExceptions = true
        };
    }

    /// <summary>
    /// Gets a friendly display name for a type
    /// </summary>
    public static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(bool)) return "Boolean";
        if (type == typeof(byte)) return "Byte";
        if (type == typeof(sbyte)) return "SByte";
        if (type == typeof(short)) return "Int16";
        if (type == typeof(ushort)) return "UInt16";
        if (type == typeof(int)) return "Int32";
        if (type == typeof(uint)) return "UInt32";
        if (type == typeof(long)) return "Int64";
        if (type == typeof(ulong)) return "UInt64";
        if (type == typeof(float)) return "Single";
        if (type == typeof(double)) return "Double";
        if (type == typeof(decimal)) return "Decimal";
        if (type == typeof(string)) return "String";
        if (type == typeof(DateTime)) return "DateTime";

        // Handle nullable types
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return GetFriendlyTypeName(type.GetGenericArguments()[0]) + "?";
        }

        return type.Name;
    }

    /// <summary>
    /// Checks if a type is an integer type
    /// </summary>
    public static bool IsIntegerType(Type? propertyType)
    {
        return propertyType == typeof(int) || propertyType == typeof(int?) ||
               propertyType == typeof(long) || propertyType == typeof(long?) ||
               propertyType == typeof(uint) || propertyType == typeof(uint?) ||
               propertyType == typeof(ulong) || propertyType == typeof(ulong?) ||
               propertyType == typeof(short) || propertyType == typeof(short?) ||
               propertyType == typeof(ushort) || propertyType == typeof(ushort?) ||
               propertyType == typeof(byte) || propertyType == typeof(byte?) ||
               propertyType == typeof(sbyte) || propertyType == typeof(sbyte?);
    }

    /// <summary>
    /// Checks if a type is numeric
    /// </summary>
    public static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }
}