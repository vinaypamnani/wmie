using System.Windows;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.PropertyGrid;
using WmiExplorer.PropertyGrid.Editors.Core;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.PropertyGrid;

public class WmiDateTimePropertyEditor : WmiPropertyEditorBase
{
    public WmiDateTimePropertyEditor(IWmiService wmiService, IMessengerService messengerService)
        : base(wmiService, messengerService) { }

    public override bool CanHandle(PropertyHierarchyItem propertyItem)
    {
        return propertyItem?.PropertyDescriptor is WmiPropertyDescriptor wmiDescriptor && wmiDescriptor.PropertyData.Type == System.Management.CimType.DateTime;
    }

    public override UIElement CreateEditor(PropertyHierarchyItem propertyItem)
    {
        if (propertyItem?.PropertyDescriptor is not WmiPropertyDescriptor wmiDescriptor)
            throw new ArgumentException("PropertyItem must have a WmiPropertyDescriptor", nameof(propertyItem));
        var textBox = PropertyEditorUtils.CreateStandardTextBox(
            wmiDescriptor.PropertyData.Value?.ToString(),
            "Enter WMI DateTime (e.g., 20231201120000.000000+060)",
            propertyItem,
            null,
            ValidateWmiDateTime
        );
        textBox.IsReadOnly = wmiDescriptor.IsReadOnly;
        return textBox;
    }

    private static bool IsValidWmiDateTime(string dateTimeString)
    {
        if (string.IsNullOrEmpty(dateTimeString))
            return false;
        try
        {
            var dateTime = System.Management.ManagementDateTimeConverter.ToDateTime(dateTimeString);
            return true;
        }
        catch
        {
            return DateTime.TryParse(dateTimeString, out _);
        }
    }

    private static ValidationManager.ValidationResult ValidateWmiDateTime(string text, object? originalValue)
    {
        if (string.IsNullOrEmpty(text))
            return ValidationManager.ValidationResult.Valid(null, !AreWmiValuesEqual(originalValue, null));
        if (IsValidWmiDateTime(text))
            return ValidationManager.ValidationResult.Valid(text, !AreWmiValuesEqual(originalValue, text));
        return ValidationManager.ValidationResult.Error("Invalid WMI DateTime format. Expected format: YYYYMMDDHHMMSS.mmmmmm±UUU (e.g., 20250708120000.000000-000)");
    }
}