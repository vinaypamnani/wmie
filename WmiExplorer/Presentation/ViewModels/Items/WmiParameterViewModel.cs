using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

public partial class WmiParameterViewModel : DisposableObservableObject
{
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

        // Initialize the display text
        UpdateObjectDisplayText();
    }

    public string? CimType => _wmiParameter.CimType;
    public string? Description => _wmiParameter.Description;
    public int Id => _wmiParameter.Id;
    public bool IsArray => _wmiParameter.IsArray;
    public bool IsEnabled => IsSelected;
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
            var result = Views.Dialogs.PropertyEditorDialog.ShowEditor(window, _parameterObject, $"Edit {TargetClassName}");
            if (result != null)
            {
                // Clean the object using the factory and update the parameter value
                Value = WmiObjectFactory.CleanParameterObject((ManagementObject)result);
                System.Diagnostics.Debug.WriteLine($"[WmiParameterViewModel] Updated parameter {Name} with edited (clean) object");
            }
        }
    }

    private bool EditObjectCanExecute()
    {
        return IsObject && !string.IsNullOrEmpty(TargetClassName) && _managementScope != null;
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
                UpdateObjectDisplayText(); // Update display text to show "configured"
                System.Diagnostics.Debug.WriteLine($"[WmiParameterViewModel] Parameter object initialized for {className} with {_parameterObject.Properties.Count} properties");
            }
        }
        catch (Exception ex)
        {
            ObjectDisplayText = $"Error initializing {TargetClassName} object: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[WmiParameterViewModel] Error initializing parameter object: {ex.Message}");
        }
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
            // CimType for references typically looks like "ref:ClassName" or just "ClassName"
            var referenceClassName = TargetClassName;
            if (string.IsNullOrEmpty(referenceClassName))
                return;

            // Load instances of the reference class
            var instances = await _wmiService.GetInstancesAsync(_managementScope, referenceClassName, _referenceValuesCts.Token);

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
            System.Diagnostics.Debug.WriteLine("[WmiParameterViewModel] Reference value loading cancelled.");
        }
        catch (Exception ex)
        {
            ReferenceLoadState = ReferenceValueLoadState.Error;
            System.Diagnostics.Debug.WriteLine($"[WmiParameterViewModel] Error loading reference values: {ex.Message}");
        }
    }

    private bool LoadReferenceValuesCanExecute()
    {
        return IsReference && _wmiService != null && _managementScope != null;
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
}

public enum ReferenceValueLoadState
{
    None,
    Loading,
    Loaded,
    Cancelled,
    Error
}