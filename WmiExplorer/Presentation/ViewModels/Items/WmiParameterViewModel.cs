using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

public partial class WmiParameterViewModel : DisposableObservableObject, IDisposable
{
    /// <summary>
    /// Gets or sets whether integer values should be displayed in hexadecimal format
    /// </summary>
    [ObservableProperty]
    private bool _isHexadecimal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled))]
    private bool _isSelected = false;

    private readonly ManagementScope? _managementScope;

    [ObservableProperty]
    private string _objectDisplayText = string.Empty;

    private ManagementObject? _parameterObject;

    [ObservableProperty]
    private ReferenceValueLoadState _referenceLoadState = ReferenceValueLoadState.None;

    [ObservableProperty]
    private ObservableCollection<string> _referenceValues = new();

    private CancellationTokenSource? _referenceValuesCts;

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

        // Initialize hex display for large values (> 0x80000000)
        InitializeHexDisplay();

        // Initialize the display text
        UpdateObjectDisplayText();
    }

    public string? CimType => _wmiParameter.CimType;
    public string? Description => _wmiParameter.Description;

    /// <summary>
    /// Gets the display value (formatted as hex or decimal based on IsHexadecimal)
    /// </summary>
    public string DisplayValue
    {
        get => GetDisplayValue();
        set => SetDisplayValue(value);
    }

    public int Id => _wmiParameter.Id;
    public bool IsArray => _wmiParameter.IsArray;
    public bool IsEnabled => IsSelected;

    /// <summary>
    /// Gets whether this parameter is an integer type that supports hex/decimal display
    /// </summary>
    public bool IsInteger => IsIntegerType(CimType);

    public bool IsObject => string.Equals(Type, "object", StringComparison.OrdinalIgnoreCase);
    public bool IsReference => string.Equals(Type, "reference", StringComparison.OrdinalIgnoreCase);
    public string? Name => _wmiParameter.Name;

    /// <summary>
    /// Gets the target WMI class name for object and reference parameters.
    /// </summary>
    public string? TargetClassName => GetTargetClassName();

    public string? Type => _wmiParameter.Type;
    public WmiParameter WmiParameter => _wmiParameter;

    /// <summary>
    /// Command to cancel the loading of reference values
    /// </summary>
    [RelayCommand(CanExecute = nameof(CancelLoadReferenceValuesCanExecute))]
    private void CancelLoadReferenceValues()
    {
        _referenceValuesCts?.Cancel();
    }

    private bool CancelLoadReferenceValuesCanExecute()
    {
        return ReferenceLoadState == ReferenceValueLoadState.Loading;
    }

    /// <summary>
    /// Converts a long value to the appropriate target type based on CimType
    /// </summary>
    private object ConvertToTargetType(long value)
    {
        return CimType?.ToLowerInvariant() switch
        {
            "uint8" => (byte)value,
            "sint8" => (sbyte)value,
            "uint16" => (ushort)value,
            "sint16" => (short)value,
            "uint32" => (uint)value,
            "sint32" => (int)value,
            "uint64" => (ulong)value,
            "sint64" => value,
            _ => value
        };
    }

    /// <summary>
    /// Command to edit object parameters using PropertyEditorDialog
    /// </summary>
    [RelayCommand(CanExecute = nameof(EditObjectCanExecute))]
    private void EditObject()
    {
        if (_parameterObject == null)
        {
            InitializeParameterObject();
        }

        if (_parameterObject != null)
        {
            // Find the parent window to use as owner
            var window = System.Windows.Application.Current.MainWindow;

            // Show the PropertyEditorDialog
            var result = Views.Dialogs.PropertyEditorDialog.ShowEditor(window, _parameterObject, $"Edit {TargetClassName}"); if (result != null)
            {
                // Clean the object using the factory and update the parameter value
                Value = WmiObjectFactory.CleanParameterObject((ManagementObject)result);
                Log.Debug("Updated parameter {ParameterName} with edited (clean) object", Name ?? "Unknown");
            }
        }
    }

    private bool EditObjectCanExecute()
    {
        return IsObject && !string.IsNullOrEmpty(TargetClassName) && _managementScope != null;
    }

    /// <summary>
    /// Gets the display value formatted as hex or decimal based on IsHexadecimal setting
    /// </summary>
    private string GetDisplayValue()
    {
        if (!IsInteger || Value == null)
            return Value?.ToString() ?? string.Empty;

        if (!IsHexadecimal)
            return Value.ToString() ?? string.Empty;

        // Format as hexadecimal
        try
        {
            var longValue = Convert.ToInt64(Value);
            return $"0x{longValue:X}";
        }
        catch
        {
            return Value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Extracts the WMI class name from the CimType qualifier.
    /// </summary>    /// <summary>
    /// Extracts the target class name from CimType or parameter qualifiers.
    /// Handles both object and reference parameter types.
    /// </summary>
    private string? GetTargetClassName()
    {
        // Try to get class name from CimType (handles multiple patterns)
        if (!string.IsNullOrEmpty(CimType))
        {
            var cimType = CimType;

            // Handle "object:ClassName" pattern
            if (cimType.Contains(':'))
            {
                var parts = cimType.Split(':');
                if (parts.Length > 1)
                {
                    return parts[1]; // Return the class name after the colon
                }
            }

            // Handle direct class name (but skip generic type names)
            if (!cimType.Equals("reference", StringComparison.OrdinalIgnoreCase) &&
                !cimType.Equals("object", StringComparison.OrdinalIgnoreCase))
            {
                return cimType;
            }
        }

        // Look for class information in qualifiers
        if (_wmiParameter.Qualifiers != null)
        {
            foreach (System.Management.QualifierData qualifier in _wmiParameter.Qualifiers)
            {
                // Check for common reference and object qualifiers
                if (qualifier.Name.Equals("CIMTYPE", StringComparison.OrdinalIgnoreCase) ||
                    qualifier.Name.Equals("REF", StringComparison.OrdinalIgnoreCase))
                {
                    var qualifierValue = qualifier.Value?.ToString();
                    if (!string.IsNullOrEmpty(qualifierValue))
                    {
                        // Handle "ref:ClassName" or "object:ClassName" patterns
                        if (qualifierValue.Contains(':'))
                        {
                            var parts = qualifierValue.Split(':');
                            if (parts.Length > 1)
                            {
                                return parts[1];
                            }
                        }

                        // Return direct value if it's not a generic type
                        if (!qualifierValue.Equals("reference", StringComparison.OrdinalIgnoreCase) &&
                            !qualifierValue.Equals("object", StringComparison.OrdinalIgnoreCase))
                        {
                            return qualifierValue;
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Initializes the hex display setting based on the current value
    /// Defaults to hex if the value is larger than 0x80000000
    /// </summary>
    private void InitializeHexDisplay()
    {
        if (!IsInteger || Value == null)
        {
            IsHexadecimal = false;
            return;
        }

        try
        {
            var longValue = Convert.ToInt64(Value);
            // Default to hex for values larger than 0x80000000 (2147483648)
            IsHexadecimal = longValue >= 0x80000000;
        }
        catch
        {
            IsHexadecimal = false;
        }
    }

    /// <summary>
    /// Initializes a parameter object for editing using WmiObjectFactory.
    /// </summary>
    private void InitializeParameterObject()
    {
        if (!IsObject || string.IsNullOrEmpty(TargetClassName) || _parameterObject != null || _managementScope == null)
            return;

        try
        {
            var className = TargetClassName;
            _parameterObject = WmiObjectFactory.CreateTemplateObject(className, _managementScope);

            // Update the parameter value to reference the created object
            if (_parameterObject != null)
            {
                Value = _parameterObject;

                // Update display text to show "configured"
                UpdateObjectDisplayText();

                Log.Debug("Parameter object initialized for {ClassName} with {PropertyCount} properties", className, _parameterObject.Properties.Count);
            }
        }
        catch (Exception ex)
        {
            ObjectDisplayText = $"Error initializing {TargetClassName} object: {ex.Message}";
            Log.Error(ex, "Error initializing parameter object for {ClassName}", TargetClassName ?? "Unknown");
        }
    }

    /// <summary>
    /// Determines if the given CIM type is an integer type that supports hex/decimal display
    /// </summary>
    private static bool IsIntegerType(string? cimType)
    {
        if (string.IsNullOrEmpty(cimType))
            return false;

        return cimType.Equals("uint8", StringComparison.OrdinalIgnoreCase) ||
               cimType.Equals("sint8", StringComparison.OrdinalIgnoreCase) ||
               cimType.Equals("uint16", StringComparison.OrdinalIgnoreCase) ||
               cimType.Equals("sint16", StringComparison.OrdinalIgnoreCase) ||
               cimType.Equals("uint32", StringComparison.OrdinalIgnoreCase) ||
               cimType.Equals("sint32", StringComparison.OrdinalIgnoreCase) ||
               cimType.Equals("uint64", StringComparison.OrdinalIgnoreCase) ||
               cimType.Equals("sint64", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Command to load reference values for reference-type parameters
    /// </summary>
    [RelayCommand(CanExecute = nameof(LoadReferenceValuesCanExecute))]
    private async Task LoadReferenceValuesAsync()
    {
        if (_wmiService == null || _managementScope == null || !IsReference)
            return;

        _referenceValuesCts?.Dispose();
        _referenceValuesCts = new CancellationTokenSource();

        try
        {
            // Set loading state
            ReferenceLoadState = ReferenceValueLoadState.Loading;

            // Extract the reference class name from the CimType
            var referenceClassName = TargetClassName;
            if (string.IsNullOrEmpty(referenceClassName))
                return;

            // Build WQL query for instances of the reference class
            string wqlQuery = $"SELECT * FROM {referenceClassName}";

            // Execute the WQL query using the service
            var instances = await _wmiService.ExecuteWmiQueryAsync(
                _managementScope,
                wqlQuery,
                false, // directRead: false for instance enumeration
                false, // useAmendedQualifiers: false for instances
                _referenceValuesCts.Token);

            // Convert instances to string representations for the ComboBox
            var referenceStrings = new List<string>();
            foreach (var instance in instances)
            {
                try
                {
                    // Try to get a meaningful string representation
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
                    Log.Warning(ex, "Error processing reference instance for parameter {ParameterName}", Name ?? "Unknown");
                }
            }

            // Update the ReferenceValues collection on UI thread
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ReferenceValues.Clear();
                foreach (var refValue in referenceStrings.OrderBy(s => s))
                {
                    ReferenceValues.Add(refValue);
                }
                Value = ReferenceValues.FirstOrDefault(); // Set the first value as default
                ReferenceLoadState = ReferenceValueLoadState.Loaded;
            });
        }
        catch (OperationCanceledException)
        {
            ReferenceLoadState = ReferenceValueLoadState.Cancelled;
            Log.Warning("Reference value loading cancelled for parameter {ParameterName}", Name ?? "Unknown");
        }
        catch (Exception ex)
        {
            ReferenceLoadState = ReferenceValueLoadState.Error;
            Log.Error(ex, "Error loading reference values for parameter {ParameterName}", Name ?? "Unknown");
        }
    }

    private bool LoadReferenceValuesCanExecute()
    {
        return IsReference && _wmiService != null && _managementScope != null;
    }

    /// <summary>
    /// Handles property change for IsHexadecimal to notify UI that DisplayValue has changed
    /// </summary>
    partial void OnIsHexadecimalChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
    }

    /// <summary>
    /// Handles property change for IsSelected to initialize object wrapper if needed
    /// </summary>
    partial void OnIsSelectedChanged(bool value)
    {
        if (value && IsObject && !string.IsNullOrEmpty(TargetClassName) && _parameterObject == null)
        {
            InitializeParameterObject();
        }
    }

    /// <summary>
    /// Handles property change for Value to update the underlying WmiParameter
    /// </summary>
    partial void OnValueChanged(object? value)
    {
        _wmiParameter.Value = value;

        if (IsObject)
        {
            UpdateObjectDisplayText();
        }
    }

    /// <summary>
    /// Sets the display value, parsing from hex or decimal format
    /// </summary>
    private void SetDisplayValue(string displayValue)
    {
        if (!IsInteger)
        {
            // For non-integer types, set the value directly
            Value = displayValue;
            return;
        }

        if (string.IsNullOrWhiteSpace(displayValue))
        {
            Value = null;
            return;
        }

        try
        {
            // Try to parse as hex if it starts with 0x
            if (displayValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var hexValue = displayValue.Substring(2);
                var longValue = Convert.ToInt64(hexValue, 16);
                Value = ConvertToTargetType(longValue);
            }
            else
            {
                // Parse as decimal
                var longValue = Convert.ToInt64(displayValue);
                Value = ConvertToTargetType(longValue);
            }
        }
        catch
        {
            // If parsing fails, keep the original string value
            Value = displayValue;
        }
    }

    /// <summary>
    /// Updates the ObjectDisplayText based on the current state
    /// </summary>
    private void UpdateObjectDisplayText()
    {
        if (!IsObject)
        {
            ObjectDisplayText = string.Empty;
            return;
        }

        if (string.IsNullOrEmpty(TargetClassName))
        {
            ObjectDisplayText = "Object parameter (type unknown) - Not supported";
            return;
        }

        if (_parameterObject == null)
        {
            ObjectDisplayText = $"{TargetClassName} object (not configured)";
            return;
        }

        ObjectDisplayText = $"{TargetClassName} object (configured)";
    }

    #region IDisposable

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _parameterObject?.Dispose();
            _referenceValuesCts?.Cancel();
            _referenceValuesCts?.Dispose();
            if (_wmiParameter is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch { }
            }
            ReferenceValues?.Clear();
        }
        base.Dispose(disposing);
    }

    #endregion
}

public enum ReferenceValueLoadState
{
    None,
    Loading,
    Loaded,
    Cancelled,
    Error
}