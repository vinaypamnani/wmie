using System.Windows;
using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Provides a content control that displays an appropriate editor for a property based on its type.
/// This is the base implementation focused on core editing functionality.
/// </summary>
public class PropertyEditor : ContentControl, IPropertyEditor
{
    public static readonly DependencyProperty PropertyItemProperty =
        DependencyProperty.Register(nameof(PropertyItem), typeof(PropertyHierarchyItem), typeof(PropertyEditor),
            new PropertyMetadata(null, OnPropertyItemChanged));

    static PropertyEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyEditor),
            new FrameworkPropertyMetadata(typeof(PropertyEditor)));
    }

    /// <summary>
    /// Gets or sets the property item to edit.
    /// </summary>
    public PropertyHierarchyItem PropertyItem
    {
        get => (PropertyHierarchyItem)GetValue(PropertyItemProperty);
        set => SetValue(PropertyItemProperty, value);
    }

    /// <summary>
    /// Determines whether this editor can handle the specified property item.
    /// The base PropertyEditor can handle any property as a fallback.
    /// </summary>
    /// <param name="propertyItem">The property item to check</param>
    /// <returns>Always true for the base editor (fallback)</returns>
    public virtual bool CanHandle(PropertyHierarchyItem propertyItem)
    {
        // Base editor can handle any property as a fallback
        return true;
    }

    /// <summary>
    /// Creates an editor UI element for the specified property item.
    /// </summary>
    /// <param name="propertyItem">The property item to create an editor for</param>
    /// <returns>A UI element that can edit the property</returns>
    public virtual UIElement CreateEditor(PropertyHierarchyItem propertyItem)
    {
        return CreateCoreEditor(propertyItem);
    }

    /// <summary>
    /// Creates the core editor for the property. This method can be used by derived classes.
    /// </summary>
    protected UIElement CreateCoreEditor(PropertyHierarchyItem propertyItem)
    {
        // Handle read-only properties
        if (propertyItem.IsReadOnly)
        {
            return PropertyEditorUtils.CreateReadOnlyEditor(propertyItem);
        }

        var propertyType = propertyItem.PropertyType;

        // Check for array types first
        if (propertyType != null && propertyType.IsArray)
        {
            return PropertyEditorUtils.CreateArrayEditor(propertyItem, propertyType);
        }

        if (propertyType == typeof(bool) || propertyType == typeof(bool?))
        {
            return PropertyEditorUtils.CreateBooleanEditor(propertyItem);
        }
        else if (propertyType != null && propertyType.IsEnum)
        {
            return PropertyEditorUtils.CreateEnumEditor(propertyItem, propertyType);
        }
        else if (PropertyEditorUtils.IsIntegerType(propertyType))
        {
            return PropertyEditorUtils.CreateIntegerEditor(propertyItem);
        }
        else if (propertyType == typeof(double) || propertyType == typeof(float) || propertyType == typeof(decimal))
        {
            return PropertyEditorUtils.CreateDecimalEditor(propertyItem, propertyType);
        }
        else if (propertyType == typeof(string))
        {
            return PropertyEditorUtils.CreateStringEditor(propertyItem);
        }
        else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
        {
            return PropertyEditorUtils.CreateDateTimeEditor(propertyItem);
        }
        else
        {
            return CreateDefaultEditor(propertyItem);
        }
    }

    /// <summary>
    /// Called when the PropertyItem changes. Can be overridden by derived classes.
    /// </summary>
    protected virtual void OnPropertyItemChanged(PropertyHierarchyItem? newPropertyItem)
    {
        UpdateEditor(newPropertyItem);
    }

    private UIElement CreateDefaultEditor(PropertyHierarchyItem propertyItem)
    {
        // CreateStandardTextBox now handles validation automatically
        return PropertyEditorUtils.CreateStandardTextBox(propertyItem.FormattedValue, $"Enter {PropertyEditorUtils.GetFriendlyTypeName(propertyItem.PropertyType)} value", propertyItem);
    }

    private static void OnPropertyItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PropertyEditor editor && e.NewValue is PropertyHierarchyItem propertyItem)
        {
            editor.OnPropertyItemChanged(propertyItem);
        }
    }

    private void UpdateEditor(PropertyHierarchyItem? propertyItem)
    {
        if (propertyItem == null)
        {
            Content = null;
            return;
        }

        // First, try to find a specialized editor
        var specializedEditor = PropertyEditorRegistry.Instance.GetEditor(propertyItem);
        if (specializedEditor != null && specializedEditor != this)
        {
            Content = specializedEditor.CreateEditor(propertyItem);
        }
        else
        {
            // Fall back to the core editor
            Content = CreateCoreEditor(propertyItem);
        }
    }
}