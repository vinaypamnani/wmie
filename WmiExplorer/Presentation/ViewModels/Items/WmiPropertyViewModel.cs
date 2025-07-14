using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

/// <summary>
/// ViewModel for WMI properties and parameters that supports object and reference editing
/// </summary>
public partial class WmiPropertyViewModel : MessagingViewModelBase, IDisposable
{
    /// <summary>
    /// Gets or sets whether integer values should be displayed in hexadecimal format
    /// </summary>
    [ObservableProperty]
    private bool _isHexadecimal;

    private readonly bool _isMethodParameter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled))]
    private bool _isSelected = false;

    private readonly ManagementScope? _managementScope;

    [ObservableProperty]
    private string _objectDisplayText = string.Empty;

    private readonly PropertyData _propertyData;

    [ObservableProperty]
    private ReferenceValueLoadState _referenceLoadState = ReferenceValueLoadState.None;

    [ObservableProperty]
    private ObservableCollection<string> _referenceValues = new();

    private CancellationTokenSource? _referenceValuesCts;

    [ObservableProperty]
    private object? _value;

    private readonly IWmiService _wmiService;

    // Remove the manual command property and initialization
    // public IAsyncRelayCommand<object?> EditObjectCommand { get; }

    public WmiPropertyViewModel(PropertyData propertyData, IWmiService? wmiService = null, ManagementScope? managementScope = null, bool isMethodParameter = false, IMessengerService? messengerService = null)
        : base(messengerService ?? throw new ArgumentNullException(nameof(messengerService)))
    {
        _propertyData = propertyData ?? throw new ArgumentNullException(nameof(propertyData));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _value = propertyData.Value;

        _managementScope = managementScope;
        _isMethodParameter = isMethodParameter;

        // Initialize hex display for large values (> 0x80000000)
        InitializeHexDisplay();

        // Initialize the display text
        UpdateObjectDisplayText();
    }

    public string? CimType
    {
        get
        {
            // First try to get CIMTYPE qualifier which has more detailed info
            if (_propertyData.Qualifiers != null)
            {
                foreach (QualifierData qualifier in _propertyData.Qualifiers)
                {
                    if (qualifier.Name.Equals("CIMTYPE", StringComparison.OrdinalIgnoreCase))
                    {
                        return qualifier.Value?.ToString();
                    }
                }
            }
            // Fallback to the basic type
            return _propertyData.Type.ToString();
        }
    }

    public string? Description
    {
        get
        {
            if (_propertyData.Qualifiers != null)
            {
                foreach (QualifierData qualifier in _propertyData.Qualifiers)
                {
                    if (qualifier.Name.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    {
                        return qualifier.Value?.ToString();
                    }
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the display value (formatted as hex or decimal based on IsHexadecimal)
    /// </summary>
    public string DisplayValue
    {
        get => GetDisplayValue();
        set => SetDisplayValue(value);
    }

    public bool IsArray => _propertyData.IsArray;
    public bool IsEnabled => IsSelected;

    /// <summary>
    /// Gets whether this parameter is an integer type that supports hex/decimal display
    /// </summary>
    public bool IsInteger => IsIntegerType(CimType);

    public bool IsObject => _propertyData.Type == System.Management.CimType.Object;
    public bool IsReference => _propertyData.Type == System.Management.CimType.Reference;
    public string? Name => _propertyData.Name;
    public PropertyData PropertyData => _propertyData;

    /// <summary>
    /// Gets the current reference text value for display and editing.
    /// </summary>
    public string ReferenceText
    {
        get => _propertyData.Value?.ToString() ?? string.Empty;
        set
        {
            _propertyData.Value = value;
            Value = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets the target WMI class name for object and reference parameters.
    /// </summary>
    public string? TargetClassName => GetTargetClassName();

    public string? Type => _propertyData.Type.ToString();

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
        if (_propertyData.Value == null)
        {
            InitializeObjectValue();
        }

        if (_propertyData.Value is ManagementBaseObject currentObject)
        {
            var window = System.Windows.Application.Current.MainWindow;
            var result = Views.Dialogs.PropertyEditorDialog.ShowEditor(window, currentObject, _messengerService, $"Edit {TargetClassName}", _wmiService, false);
            if (result != null)
            {
                if (_isMethodParameter)
                {
                    // For method parameters, clean the object for WMI method calls
                    Value = WmiObjectFactory.CleanParameterObject((ManagementBaseObject)result);
                    Log.Debug("Updated {ItemType} {ItemName} with edited and cleaned object for method parameter", GetItemType(), Name ?? "Unknown");
                }
                else
                {
                    // For property values, store the ManagementObject directly without "cleaning"
                    Value = (ManagementBaseObject)result;
                    Log.Debug("Updated {ItemType} {ItemName} with edited object for property value", GetItemType(), Name ?? "Unknown");
                }
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
    /// Gets a descriptive name for the type of item for logging
    /// </summary>
    private string GetItemType()
    {
        return "property";
    }

    /// <summary>
    /// Extracts the target class name from qualifiers or existing object value.
    /// </summary>
    private string? GetTargetClassName()
    {
        // Check the CIMTYPE qualifier which contains the reference class name
        if (_propertyData.Qualifiers != null)
        {
            foreach (QualifierData qualifier in _propertyData.Qualifiers)
            {
                if (qualifier.Name.Equals("CIMTYPE", StringComparison.OrdinalIgnoreCase))
                {
                    var cimTypeValue = qualifier.Value?.ToString();

                    if (!string.IsNullOrEmpty(cimTypeValue))
                    {
                        // Handle "ref:ClassName" or "object:ClassName" patterns
                        if (cimTypeValue.Contains(':'))
                        {
                            var parts = cimTypeValue.Split(':');
                            if (parts.Length > 1)
                            {
                                return parts[1]; // Return the class name after the colon
                            }
                        }

                        // Handle direct class name (but skip generic type names)
                        if (!cimTypeValue.Equals("reference", StringComparison.OrdinalIgnoreCase) &&
                            !cimTypeValue.Equals("object", StringComparison.OrdinalIgnoreCase))
                        {
                            return cimTypeValue;
                        }
                    }
                    break;
                }
            }
        }

        // Fallback: Try to get class name from CimType (handles multiple patterns)
        if (!string.IsNullOrEmpty(CimType))
        {
            var cimType = CimType;

            // Handle "object:ClassName" or "reference:ClassName" pattern
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

        // For object properties, also check if there's an existing object value
        if (IsObject && _propertyData.Value is ManagementBaseObject mbo)
        {
            return mbo.ClassPath?.ClassName;
        }

        return null;
    }

    /// <summary>
    /// Initialize hex display for integer values that are likely to be displayed as hex
    /// </summary>
    private void InitializeHexDisplay()
    {
        if (!IsInteger || Value == null)
            return;

        try
        {
            var longValue = Convert.ToInt64(Value);
            // Default to hex for values > 0x80000000 (2,147,483,648)
            IsHexadecimal = longValue > 0x80000000;
        }
        catch
        {
            // If conversion fails, default to decimal
        }
    }

    private void InitializeObjectValue()
    {
        if (!IsObject || string.IsNullOrEmpty(TargetClassName) || _propertyData.Value != null || _managementScope == null)
            return;

        try
        {
            var className = TargetClassName;
            ManagementObject? newObject = null;

            if (_propertyData.Value is ManagementBaseObject existingObject)
            {
                newObject = WmiObjectFactory.CreateTemplateObject(className, _managementScope);
                if (newObject != null && existingObject.Properties != null)
                {
                    foreach (PropertyData existingProp in existingObject.Properties)
                    {
                        try
                        {
                            if (newObject.Properties[existingProp.Name] != null)
                            {
                                newObject.Properties[existingProp.Name].Value = existingProp.Value;
                            }
                        }
                        catch (Exception propEx)
                        {
                            Log.Warning(propEx, "Failed to copy property {PropertyName} from existing object", existingProp.Name);
                        }
                    }
                }
                Log.Debug("Parameter object initialized from existing value for {ClassName} with {PropertyCount} properties", className, newObject?.Properties.Count ?? 0);
            }
            else
            {
                newObject = WmiObjectFactory.CreateTemplateObject(className, _managementScope);
                Log.Debug("Parameter object initialized as new template for {ClassName} with {PropertyCount} properties", className, newObject?.Properties.Count ?? 0);
            }

            if (newObject != null)
            {
                Value = newObject;
                UpdateObjectDisplayText();
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
                        referenceStrings.Add(instance.ToString() ?? string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error processing reference instance for {ItemType} {ItemName}", GetItemType(), Name ?? "Unknown");
                }
            }

            // Update the ReferenceValues collection on UI thread
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Track the current selected value before updating
                var previousValue = Value as string;

                ReferenceValues.Clear();

                // Check if there's an existing reference value that should be included
                var existingValue = _propertyData.Value?.ToString();
                var allValues = new List<string>(referenceStrings);

                // Add existing value if it's not already in the list
                if (!string.IsNullOrEmpty(existingValue) && !allValues.Contains(existingValue))
                {
                    allValues.Add(existingValue);
                    Log.Debug("Added existing reference value '{ExistingValue}' to available options", existingValue);
                }

                // Populate the collection with sorted values
                foreach (var refValue in allValues.OrderBy(s => s))
                {
                    ReferenceValues.Add(refValue);
                }

                // After populating ReferenceValues, restore selection
                if (!string.IsNullOrEmpty(previousValue) && ReferenceValues.Contains(previousValue))
                {
                    Value = previousValue;
                }
                else if (ReferenceValues.Count > 0)
                {
                    Value = ReferenceValues[0];
                }

                ReferenceLoadState = ReferenceValueLoadState.Loaded;
                Log.Debug("Loaded {Count} reference values for {ItemType} {ItemName}", ReferenceValues.Count, GetItemType(), Name ?? "Unknown");
            });
        }
        catch (OperationCanceledException)
        {
            ReferenceLoadState = ReferenceValueLoadState.Cancelled;
            Log.Warning("Reference value loading cancelled for {ItemType} {ItemName}", GetItemType(), Name ?? "Unknown");
        }
        catch (Exception ex)
        {
            ReferenceLoadState = ReferenceValueLoadState.Error;
            Log.Error(ex, "Error loading reference values for {ItemType} {ItemName}", GetItemType(), Name ?? "Unknown");
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
        if (value && IsObject && !string.IsNullOrEmpty(TargetClassName) && _propertyData.Value == null)
        {
            InitializeObjectValue();
        }
    }

    /// <summary>
    /// Handles property change for ReferenceLoadState to publish message
    /// </summary>
    partial void OnReferenceLoadStateChanged(ReferenceValueLoadState value)
    {
        PublishMessage(new ReferenceLoadStateChangedMessage(Name ?? string.Empty, value));
    }

    /// <summary>
    /// Handles property change for Value to update the underlying PropertyData
    /// </summary>
    partial void OnValueChanged(object? value)
    {
        _propertyData.Value = value;

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

        bool hasObjectValue = _propertyData.Value is ManagementBaseObject;

        if (hasObjectValue)
        {
            var valueConverter = new Integration.PropertyTypeProvider.WmiPropertyValueConverter();
            if (Value is ManagementBaseObject mbo)
            {
                ObjectDisplayText = valueConverter.ConvertToString(mbo, typeof(ManagementBaseObject));
            }
            else
            {
                ObjectDisplayText = $"Embedded: {TargetClassName} object (configured)]";
            }
        }
        else
        {
            ObjectDisplayText = $"Embedded: {TargetClassName} object (not configured)]";
        }
    }

    #region IDisposable

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_propertyData.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _referenceValuesCts?.Cancel();
            _referenceValuesCts?.Dispose();
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