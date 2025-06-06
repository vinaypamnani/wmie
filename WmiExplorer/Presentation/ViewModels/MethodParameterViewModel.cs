using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.ViewModels;

public class MethodParameterViewModel : ViewModelBase
{
    private bool _isSelected = false;
    private object? _value;

    public MethodParameterViewModel(WmiParameter model)
    {
        Model = model;
        _value = model.Value;
    }

    public string? CimType => Model.CimType;
    public string? Description => Model.Description;
    public int Id => Model.Id;
    public bool IsArray => Model.IsArray;
    public bool IsComplexType => IsComplex(Type);
    public bool IsEnabled => IsSelected && !IsComplexType;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    }

    public WmiParameter Model { get; }
    public string? Name => Model.Name;
    public string? Type => Model.Type;

    public object? Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                Model.Value = value;
            }
        }
    }

    private bool IsComplex(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return true;

        typeName = typeName.ToLowerInvariant();
        return typeName switch
        {
            "object" or "reference" => true,
            _ => false
        };
    }
}