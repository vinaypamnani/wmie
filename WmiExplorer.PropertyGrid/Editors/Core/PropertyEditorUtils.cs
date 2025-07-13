using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WmiExplorer.PropertyGrid.Editors.TypeEditors;

namespace WmiExplorer.PropertyGrid.Editors.Core;

/// <summary>
/// Simplified utility class providing common functionality for property editors.
/// This is now a thin facade over the organized editor system.
/// </summary>
public static class PropertyEditorUtils
{
    // Expose constants for backward compatibility
    public static readonly Thickness CHECKBOX_HEX_MARGIN = EditorInfrastructure.CHECKBOX_HEX_MARGIN;
    public static readonly Thickness CONTROL_MARGIN_STANDARD = EditorInfrastructure.CONTROL_MARGIN_STANDARD;
    public static readonly Thickness TIP_TEXT_MARGIN = EditorInfrastructure.TIP_TEXT_MARGIN;

    /// <summary>
    /// Applies MaxWidth constraint to any FrameworkElement based on parent container
    /// </summary>

    public static void ApplyMaxWidthConstraint(FrameworkElement element, FrameworkElement? parentContainer = null, double widthToSubtract = 20, bool forceApply = false)
        => UIHelpers.ApplyMaxWidthConstraint(element, parentContainer, widthToSubtract, forceApply);

    /// <summary>
    /// Applies standard configuration to a FrameworkElement for property editing
    /// </summary>
    public static void ApplyStandardEditorConfiguration(FrameworkElement element, PropertyHierarchyItem propertyItem, bool applyMaxWidth = true)
        => EditorInfrastructure.ApplyStandardEditorConfiguration(element, propertyItem, applyMaxWidth);

    /// <summary>
    /// Helper to attach focus event for selection
    /// </summary>
    public static void AttachSelectOnFocus(Control control, PropertyHierarchyItem? propertyItem)
        => EditorInfrastructure.AttachSelectOnFocus(control, propertyItem);

    /// <summary>
    /// Clears integer validation error state from a TextBox (backward compatibility)
    /// </summary>

    public static void ClearIntegerValidationError(TextBox textBox)
        => ValidationManager.ClearValidationError(textBox);

    /// <summary>
    /// Clears validation error state from a TextBox
    /// </summary>
    public static void ClearValidationError(TextBox textBox)
        => ValidationManager.ClearValidationError(textBox);

    /// <summary>
    /// Clears validation error state from a TextBox with original values
    /// </summary>
    public static void ClearValidationError(TextBox textBox, System.Windows.Media.Brush originalBorderBrush, object originalToolTip)
        => ValidationManager.ClearValidationError(textBox, originalBorderBrush, originalToolTip);

    /// <summary>
    /// Creates an array editor with input validation and tips
    /// </summary>

    public static StackPanel CreateArrayEditor(PropertyHierarchyItem propertyItem, Type arrayType)
        => ArrayEditor.Create(propertyItem, arrayType);

    /// <summary>
    /// Creates a standardized CheckBox for boolean property editing
    /// </summary>
    public static CheckBox CreateBooleanEditor(PropertyHierarchyItem propertyItem)
        => BooleanEditor.Create(propertyItem);

    /// <summary>
    /// Creates a complete char editor with hex/decimal support
    /// </summary>
    public static Grid CreateCharEditor(PropertyHierarchyItem propertyItem)
        => CharEditor.Create(propertyItem);

    /// <summary>
    /// Creates a standardized DatePicker for DateTime property editing
    /// </summary>
    public static DatePicker CreateDateTimeEditor(PropertyHierarchyItem propertyItem)
        => DateTimeEditor.Create(propertyItem);

    /// <summary>
    /// Creates a standardized TextBox for decimal/floating-point property editing
    /// </summary>
    public static TextBox CreateDecimalEditor(PropertyHierarchyItem propertyItem, Type propertyType)
        => NumericEditor.Create(propertyItem, propertyType);

    /// <summary>
    /// Creates a standardized ComboBox for enum property editing
    /// </summary>
    public static ComboBox CreateEnumEditor(PropertyHierarchyItem propertyItem, Type enumType)
        => EnumEditor.Create(propertyItem, enumType);

    /// <summary>
    /// Creates a standard Grid layout with main content and an action panel (single control or container)
    /// </summary>
    public static Grid CreateGridWithActionPanel(UIElement mainContent, UIElement actionPanel, double actionPanelWidth = 80)
        => EditorInfrastructure.CreateGridWithActionPanel(mainContent, actionPanel, actionPanelWidth);

    /// <summary>
    /// Creates a complete integer editor with hex/decimal support
    /// </summary>
    public static Grid CreateIntegerEditor(PropertyHierarchyItem propertyItem)
        => IntegerEditor.Create(propertyItem);

    /// <summary>
    /// Creates a read-only TextBlock for displaying property values
    /// </summary>
    public static TextBox CreateReadOnlyEditor(PropertyHierarchyItem propertyItem)
        => ReadOnlyEditor.Create(propertyItem);

    /// <summary>
    /// Creates a standard two-way binding for property values
    /// </summary>
    public static Binding CreateStandardPropertyBinding(PropertyHierarchyItem propertyItem, UpdateSourceTrigger trigger = UpdateSourceTrigger.PropertyChanged)
        => EditorInfrastructure.CreateStandardPropertyBinding(propertyItem, trigger);

    /// <summary>
    /// Creates a standardized TextBox for property editing with consistent styling and behavior
    /// </summary>
    public static TextBox CreateStandardTextBox(string? initialText = null, string? placeholder = null, PropertyHierarchyItem? propertyItem = null, Thickness? margin = null, System.Func<string, object?, ValidationManager.ValidationResult>? customValidation = null)
    {
        return UIHelpers.CreateStandardTextBox(initialText, placeholder, propertyItem, margin, customValidation);
    }

    /// <summary>
    /// Creates a standardized TextBox for string property editing
    /// </summary>
    public static TextBox CreateStringEditor(PropertyHierarchyItem propertyItem)
        => StringEditor.Create(propertyItem);

    /// <summary>
    /// Gets a friendly display name for a type
    /// </summary>
    public static string GetFriendlyTypeName(Type type)
        => EditorInfrastructure.GetFriendlyTypeName(type);

    /// <summary>
    /// Standardizes initialization for all editor controls: sets DataContext and ValidationState.
    /// </summary>
    public static T InitializeEditor<T>(T control, PropertyHierarchyItem propertyItem) where T : Control
    {
        control.DataContext = propertyItem;
        ValidationManager.SetValidationNormal(control);
        return control;
    }

    /// <summary>
    /// Checks if a type is an integer type
    /// </summary>
    public static bool IsIntegerType(Type? propertyType)
        => EditorInfrastructure.IsIntegerType(propertyType);

    /// <summary>
    /// Shows integer validation error state on a TextBox (backward compatibility)
    /// </summary>
    public static void ShowIntegerValidationError(TextBox textBox, string errorMessage)
        => ValidationManager.ShowValidationError(textBox, errorMessage);

    /// <summary>
    /// Shows integer validation success state on a TextBox for modified values (backward compatibility)
    /// </summary>
    public static void ShowIntegerValidationSuccess(TextBox textBox, string successMessage = "Value modified")
        => ValidationManager.ShowValidationSuccess(textBox, successMessage);

    /// <summary>
    /// Shows validation error state on a TextBox
    /// </summary>
    public static void ShowValidationError(TextBox textBox, string errorMessage)
        => ValidationManager.ShowValidationError(textBox, errorMessage);

    /// <summary>
    /// Shows validation success state on a TextBox for modified values
    /// </summary>
    public static void ShowValidationSuccess(TextBox textBox, string successMessage = "Value modified")
        => ValidationManager.ShowValidationSuccess(textBox, successMessage);
}