using System.Windows;
using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid.Editors.Core;

/// <summary>
/// Provides a content control that displays an appropriate editor for a property based on its type.
/// This is the base implementation focused on core editing functionality.
/// </summary>
public class PropertyEditor : ContentControl, IPropertyEditor
{
    public static readonly DependencyProperty PropertyItemProperty =
        DependencyProperty.Register(nameof(PropertyItem), typeof(PropertyHierarchyItem), typeof(PropertyEditor),
            new PropertyMetadata(null, OnPropertyItemChanged));

    public PropertyEditor()
    {
        this.Focusable = false;
        this.IsTabStop = false;
    }

    static PropertyEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyEditor),
            new FrameworkPropertyMetadata(typeof(PropertyEditor)));
        FocusableProperty.OverrideMetadata(typeof(PropertyEditor), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(PropertyEditor), new FrameworkPropertyMetadata(false));
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
        // Note: Read-only check is now handled in UpdateEditor before calling specialized editors
        // This ensures consistent behavior across all editors

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
        else if (propertyType == typeof(char) || propertyType == typeof(char?))
        {
            return PropertyEditorUtils.CreateCharEditor(propertyItem);
        }
        else
        {
            return CreateDefaultEditor(propertyItem);
        }
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == System.Windows.Input.Key.Tab)
        {
            bool moveBackward = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
            EditorTabNavigationHelper.HandleTabNavigation(this, moveBackward, e);
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

        // Check if property should be treated as read-only first
        if (PropertyEditorUtils.ShouldTreatAsReadOnly(propertyItem))
        {
            Content = PropertyEditorUtils.CreateReadOnlyEditor(propertyItem);
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