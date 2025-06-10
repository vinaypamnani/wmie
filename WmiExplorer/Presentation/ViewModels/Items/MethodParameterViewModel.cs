using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.ViewModels.Items;

public partial class MethodParameterViewModel : DisposableObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled))]
    private bool _isSelected = false;

    [ObservableProperty]
    private object? _value;

    private WmiParameter _wmiParameter;

    public MethodParameterViewModel(WmiParameter wmiParameter)
    {
        _wmiParameter = wmiParameter;
        _value = wmiParameter.Value;
    }

    public string? CimType => _wmiParameter.CimType;
    public string? Description => _wmiParameter.Description;
    public int Id => _wmiParameter.Id;
    public bool IsArray => _wmiParameter.IsArray;
    public bool IsComplexType => IsComplex();
    public bool IsEnabled => IsSelected && !IsComplexType;
    public bool IsObject => string.Equals(Type, "object", StringComparison.OrdinalIgnoreCase);
    public bool IsReference => string.Equals(Type, "reference", StringComparison.OrdinalIgnoreCase);
    public string? Name => _wmiParameter.Name;
    public string? Type => _wmiParameter.Type;
    public WmiParameter WmiParameter => _wmiParameter;

    private bool IsComplex()
    {
        // A parameter is considered complex if it is an object or reference type
        return IsObject || IsReference;
    }

    /// <summary>
    /// Handles property change for Value to update the underlying WmiParameter
    /// </summary>
    partial void OnValueChanged(object? value)
    {
        _wmiParameter.Value = value;
    }
}