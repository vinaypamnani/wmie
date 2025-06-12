using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

public partial class WmiParameterViewModel : DisposableObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled))]
    private bool _isSelected = false;

    private readonly ManagementScope? _managementScope;

    [ObservableProperty]
    private ObservableCollection<string> _referenceValues = new();

    [ObservableProperty]
    private object? _value;

    private WmiParameter _wmiParameter;
    private readonly IWmiService? _wmiService;

    public WmiParameterViewModel(WmiParameter wmiParameter, IWmiService? wmiService = null, ManagementScope? managementScope = null)
    {
        _wmiParameter = wmiParameter;
        _value = wmiParameter.Value;
        _wmiService = wmiService;
        _managementScope = managementScope;
    }

    public string? CimType => _wmiParameter.CimType;
    public string? Description => _wmiParameter.Description;
    public int Id => _wmiParameter.Id;
    public bool IsArray => _wmiParameter.IsArray;
    public bool IsEnabled => IsSelected;
    public bool IsObject => string.Equals(Type, "object", StringComparison.OrdinalIgnoreCase);
    public bool IsReference => string.Equals(Type, "reference", StringComparison.OrdinalIgnoreCase);
    public string? Name => _wmiParameter.Name;
    public string? Type => _wmiParameter.Type;
    public WmiParameter WmiParameter => _wmiParameter;

    private bool CanLoadReferenceValues()
    {
        return IsReference && _wmiService != null && _managementScope != null;
    }

    /// <summary>
    /// Extracts the reference class name from the CimType or other parameter qualifiers
    /// </summary>
    private string? ExtractReferenceClassName()
    {
        // Try CimType first - it might contain the reference class name
        if (!string.IsNullOrEmpty(CimType))
        {
            // Handle patterns like "ref:ClassName" or "ClassName"
            var cimType = CimType;
            if (cimType.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
            {
                return cimType.Substring(4);
            }

            // For some WMI parameters, the CimType might directly contain the class name
            // This is a heuristic - we could enhance this based on actual WMI behavior
            if (!cimType.Equals("reference", StringComparison.OrdinalIgnoreCase) &&
                !cimType.Equals("object", StringComparison.OrdinalIgnoreCase))
            {
                return cimType;
            }
        }

        // Look for reference class information in qualifiers
        if (_wmiParameter.Qualifiers != null)
        {
            foreach (System.Management.QualifierData qualifier in _wmiParameter.Qualifiers)
            {
                // Check for common reference qualifiers
                if (qualifier.Name.Equals("CIMTYPE", StringComparison.OrdinalIgnoreCase) ||
                    qualifier.Name.Equals("REF", StringComparison.OrdinalIgnoreCase))
                {
                    var qualifierValue = qualifier.Value?.ToString();
                    if (!string.IsNullOrEmpty(qualifierValue))
                    {
                        if (qualifierValue.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
                        {
                            return qualifierValue.Substring(4);
                        }
                        return qualifierValue;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Command to load reference values for reference-type parameters
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoadReferenceValues))]
    private async Task LoadReferenceValuesAsync()
    {
        if (_wmiService == null || _managementScope == null || !IsReference)
            return;

        try
        {
            // Extract the reference class name from the CimType
            // CimType for references typically looks like "ref:ClassName" or just "ClassName"
            var referenceClassName = ExtractReferenceClassName();
            if (string.IsNullOrEmpty(referenceClassName))
                return;

            // Load instances of the reference class
            var instances = await _wmiService.GetInstancesAsync(_managementScope, referenceClassName);

            // Convert instances to string representations for the ComboBox
            var referenceStrings = new List<string>();
            foreach (var instance in instances)
            {
                try
                {
                    // Try to get a meaningful string representation
                    // Common patterns: use __PATH, __RELPATH, or key properties
                    var path = instance.Path?.RelativePath;
                    if (!string.IsNullOrEmpty(path))
                    {
                        referenceStrings.Add(path);
                    }
                    else
                    {
                        // Fallback to string representation
                        referenceStrings.Add(instance.ToString());
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WmiParameterViewModel] Error processing reference instance: {ex.Message}");
                }
            }            // Update the ReferenceValues collection on UI thread
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ReferenceValues.Clear();
                foreach (var refValue in referenceStrings.OrderBy(s => s))
                {
                    ReferenceValues.Add(refValue);
                }
                Value = ReferenceValues.FirstOrDefault(); // Set the first value as default

            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WmiParameterViewModel] Error loading reference values: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles property change for Value to update the underlying WmiParameter
    /// </summary>
    partial void OnValueChanged(object? value)
    {
        _wmiParameter.Value = value;
    }
}