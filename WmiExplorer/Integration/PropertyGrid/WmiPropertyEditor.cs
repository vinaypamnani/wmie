using System.Windows;
using WmiExplorer.PropertyGrid;
using WmiExplorer.PropertyGrid.Abstractions;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.PropertyGrid;

public class WmiPropertyEditor : IPropertyEditor, IDisposable
{
    private readonly List<WmiPropertyEditorBase> _editors;

    public WmiPropertyEditor(IWmiService wmiService, IMessengerService messengerService)
    {
        _editors = new List<WmiPropertyEditorBase>
        {
            new WmiObjectPropertyEditor(wmiService, messengerService),
            new WmiReferencePropertyEditor(wmiService, messengerService),
            new WmiEmbeddedObjectArrayEditor(wmiService, messengerService),
            new WmiDateTimePropertyEditor(wmiService, messengerService)
        };
    }

    public bool CanHandle(PropertyHierarchyItem propertyItem)
    {
        foreach (var editor in _editors)
        {
            if (editor.CanHandle(propertyItem))
                return true;
        }
        return false;
    }

    public UIElement CreateEditor(PropertyHierarchyItem propertyItem)
    {
        foreach (var editor in _editors)
        {
            if (editor.CanHandle(propertyItem))
                return editor.CreateEditor(propertyItem);
        }
        throw new ArgumentException("No suitable editor found for the given property item.", nameof(propertyItem));
    }

    #region IDisposable

    public void Dispose()
    {
        foreach (var editor in _editors)
        {
            editor.Dispose();
        }
    }

    #endregion
}