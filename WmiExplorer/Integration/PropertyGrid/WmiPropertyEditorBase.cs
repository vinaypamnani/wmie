using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WmiExplorer.Common.Logging;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.PropertyGrid;
using WmiExplorer.PropertyGrid.Abstractions;
using WmiExplorer.PropertyGrid.Editors.Core;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.PropertyGrid;

public abstract class WmiPropertyEditorBase : IPropertyEditor, IDisposable
{
    protected readonly IMessengerService _messengerService;
    protected readonly WmiPropertyValueConverter _propertyValueConverter = new WmiPropertyValueConverter();
    protected readonly ConcurrentDictionary<string, WmiPropertyViewModel> _viewModels = new();
    protected readonly IWmiService _wmiService;

    protected WmiPropertyEditorBase(IWmiService wmiService, IMessengerService messengerService)
    {
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
    }

    public abstract bool CanHandle(PropertyHierarchyItem propertyItem);

    public abstract UIElement CreateEditor(PropertyHierarchyItem propertyItem);

    protected void ApplyValidation(Control control, PropertyHierarchyItem propertyItem)
    {
        var current = propertyItem.Value;
        var original = propertyItem.OriginalValue;
        bool isModified;
        if (current is System.Management.ManagementBaseObject mboCurrent && original is System.Management.ManagementBaseObject mboOriginal)
        {
            isModified = !AreWmiObjectsEqual(mboCurrent, mboOriginal);
        }
        else
        {
            isModified = !ValidationManager.AreValuesEqual(current, original);
        }
        if (isModified)
        {
            ValidationManager.SetValidationModified(control);
        }
        else
        {
            ValidationManager.SetValidationNormal(control);
        }
    }

    protected static bool AreWmiObjectsEqual(System.Management.ManagementBaseObject? obj1, System.Management.ManagementBaseObject? obj2)
    {
        if (obj1 == null && obj2 == null) { return true; }
        if (obj1 == null || obj2 == null) { return false; }
        if (obj1.Properties.Count != obj2.Properties.Count) { return false; }
        foreach (System.Management.PropertyData prop in obj1.Properties)
        {
            var otherProp = obj2.Properties[prop.Name];
            if (otherProp == null)
            {
                return false;
            }
            if (!ValidationManager.AreValuesEqual(prop.Value, otherProp.Value))
            {
                return false;
            }
        }
        return true;
    }

    protected static bool AreWmiValuesEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;
        return value1.ToString() == value2.ToString();
    }

    protected WmiPropertyViewModel CreateViewModel(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var scope = wmiDescriptor.GetManagementScope();
            var wmiProperty = wmiDescriptor.WmiProperty;
            return new WmiPropertyViewModel(wmiProperty, _wmiService, scope, false, _messengerService);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating ViewModel for property '{PropertyName}'", wmiDescriptor.Name);
            throw;
        }
    }

    protected WmiPropertyViewModel GetOrCreateViewModel(WmiPropertyDescriptor wmiDescriptor)
    {
        var key = $"{wmiDescriptor.Name}_{wmiDescriptor.GetHashCode()}";
        return _viewModels.GetOrAdd(key, _ => CreateViewModel(wmiDescriptor));
    }

    protected string GetReferenceText(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.ReferenceText ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    protected ObservableCollection<string> GetReferenceValues(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.ReferenceValues ?? new ObservableCollection<string>();
        }
        catch
        {
            return new ObservableCollection<string>();
        }
    }

    protected void SetReferenceText(WmiPropertyDescriptor wmiDescriptor, string value)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            viewModel.ReferenceText = value;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting reference text for property '{PropertyName}' to value '{Value}'", wmiDescriptor.Name, value);
        }
    }

    #region IDisposable
    private bool _disposed = false;

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var viewModel in _viewModels.Values)
            {
                if (viewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _viewModels.Clear();
            _disposed = true;
        }
    }

    #endregion
}