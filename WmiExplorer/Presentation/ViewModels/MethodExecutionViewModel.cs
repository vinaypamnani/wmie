using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management;
using System.Text;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.ViewModels;

/// <summary>
/// ViewModel for executing WMI methods (both static and instance)
/// </summary>
public class MethodExecutionViewModel : ViewModelBase
{
    /// <summary>
    /// Event raised when the user wants to close the dialog.
    /// </summary>
    public event EventHandler? CloseRequested;

    private readonly WmiClass _class;

    // Results from method execution
    private string _executionResults = string.Empty;

    private bool _hasOutputParameters;
    private readonly WmiInstance? _instance;
    private readonly WmiMethod _method;

    // Store the full model objects
    private readonly WmiNamespace _namespace;

    private WmiBaseObject? _outputParameters;

    // Method parameters for the UI
    private readonly ObservableCollection<WmiParameterViewModel> _parameters = new();

    private int _selectedTabIndex;

    // Properties for the UI
    private string _statusMessage = "Ready to execute method";

    private bool _isMethodDescriptionExpanded;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodExecutionViewModel"/> class.
    /// </summary>
    /// <param name="wmiNamespace">The WMI namespace containing the class.</param>
    /// <param name="wmiClass">The WMI class containing the method.</param>
    /// <param name="wmiMethod">The WMI method to execute.</param>
    /// <param name="wmiInstance">The WMI instance for non-static methods (optional).</param>
    public MethodExecutionViewModel(
        WmiNamespace wmiNamespace,
        WmiClass wmiClass,
        WmiMethod wmiMethod,
        WmiInstance? wmiInstance = null)
    {
        _namespace = wmiNamespace ?? throw new ArgumentNullException(nameof(wmiNamespace));
        _class = wmiClass ?? throw new ArgumentNullException(nameof(wmiClass));
        _method = wmiMethod ?? throw new ArgumentNullException(nameof(wmiMethod));
        _instance = wmiInstance;

        // Verify method is appropriate for the context (static vs instance)
        if (_instance == null && !_method.IsStatic)
        {
            throw new ArgumentException("Cannot execute non-static method without an instance");
        }        // Load parameters
        LoadMethodParameters();

        // Initialize commands
        ExecuteMethodCommand = new RelayCommand(ExecuteMethodWrapper);
        CancelCommand = new RelayCommand(Cancel);
        ExpandMethodDescriptionCommand = new RelayCommand(_ => IsMethodDescriptionExpanded = true);
        CollapseMethodDescriptionCommand = new RelayCommand(_ => IsMethodDescriptionExpanded = false);
    }

    /// <summary>
    /// Gets the command to cancel and close the dialog.
    /// </summary>
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Gets the command to collapse the method description.
    /// </summary>
    public ICommand CollapseMethodDescriptionCommand { get; }

    /// <summary>
    /// Gets the class name.
    /// </summary>
    public string ClassName => _class.ClassName;

    /// <summary>
    /// Gets the command to execute the method.
    /// </summary>
    public ICommand ExecuteMethodCommand { get; }

    /// <summary>
    /// Gets the command to expand the method description.
    /// </summary>
    public ICommand ExpandMethodDescriptionCommand { get; }

    /// <summary>
    /// Gets or sets the execution results.
    /// </summary>
    public string ExecutionResults
    {
        get => _executionResults;
        private set => SetProperty(ref _executionResults, value);
    }

    /// <summary>
    /// Gets a value indicating whether there are output parameters to display.
    /// </summary>
    public bool HasOutputParameters
    {
        get => _hasOutputParameters;
        private set => SetProperty(ref _hasOutputParameters, value);
    }

    /// <summary>
    /// Gets the instance name (if applicable).
    /// </summary>
    public string InstanceName => _instance?.InstanceName ?? string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the method description is expanded.
    /// </summary>
    public bool IsMethodDescriptionExpanded
    {
        get => _isMethodDescriptionExpanded;
        set => SetProperty(ref _isMethodDescriptionExpanded, value);
    }

    /// <summary>
    /// Gets a value indicating whether the method is static.
    /// </summary>
    public bool IsStaticMethod => _method.IsStatic;

    /// <summary>
    /// Gets the description of the method being executed.
    /// </summary>
    public string MethodDescription => _method.Description;

    /// <summary>
    /// Gets the name of the method being executed.
    /// </summary>
    public string MethodName => _method.Name;

    /// <summary>
    /// Gets the output parameters to display in the PropertyGrid.
    /// </summary>
    public WmiBaseObject? OutputParameters
    {
        get => _outputParameters;
        private set => SetProperty(ref _outputParameters, value);
    }

    /// <summary>
    /// Gets the collection of parameters for the method.
    /// </summary>
    public ObservableCollection<WmiParameterViewModel> Parameters => _parameters;

    /// <summary>
    /// Gets or sets the selected tab index (0 = Input, 1 = Output).
    /// </summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    /// <summary>
    /// Gets the status message to display in the status bar.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private void Cancel(object? parameter)
    {
        // To be implemented in the dialog
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteMethod()
    {
        try
        {
            // Update status
            StatusMessage = "Executing method...";

            // Reset output parameters
            HasOutputParameters = false;
            OutputParameters = null;

            if (_instance != null)
            {
                // Execute instance method
                // Create parameters for the method invocation
                ManagementBaseObject? inParams = null;

                if (_parameters.Count > 0)
                {
                    // Get the in-parameters for the method from the class
                    inParams = _class.ActualClass.GetMethodParameters(_method.Name);

                    // Set parameter values
                    foreach (var param in _parameters.Where(p => p.IsSelected))
                    {
                        if (param.Name != null && param.Value != null)
                        {
                            inParams[param.Name] = param.Value;
                        }
                    }
                }

                // Invoke the method on the instance
                var outParams = _instance.ActualObject.InvokeMethod(
                    _method.Name,
                    inParams,
                    null);

                // Convert the out parameters to a WmiParameterCollection
                var result = new WmiParameterCollection(outParams);
                ExecutionResults = FormatResults(result);

                // Update output parameters
                if (outParams != null && outParams.Properties.Count > 0)
                {
                    OutputParameters = new WmiBaseObject(outParams);
                    HasOutputParameters = true;
                }                // Update status
                StatusMessage = "Method executed successfully";

                // Switch to Output tab to show results
                SelectedTabIndex = 1;
            }
            else
            {
                // Execute static method
                // Create parameters for the method invocation
                ManagementBaseObject? inParams = null;

                if (_parameters.Count > 0)
                {
                    // Get the in-parameters for the method from the class
                    inParams = _class.ActualClass.GetMethodParameters(_method.Name);

                    // Set parameter values
                    foreach (var param in _parameters.Where(p => p.IsSelected))
                    {
                        if (param.Name != null && param.Value != null)
                        {
                            inParams[param.Name] = param.Value;
                        }
                    }
                }

                // Invoke the static method on the class
                var outParams = _class.ActualClass.InvokeMethod(
                    _method.Name,
                    inParams,
                    null);

                // Convert the out parameters to a WmiParameterCollection
                var result = new WmiParameterCollection(outParams);
                ExecutionResults = FormatResults(result);

                // Update output parameters
                if (outParams != null && outParams.Properties.Count > 0)
                {
                    OutputParameters = new WmiBaseObject(outParams);
                    HasOutputParameters = true;
                }

                // Update status
                StatusMessage = "Method executed successfully";

                // Switch to Output tab to show results
                SelectedTabIndex = 1;
            }
        }
        catch (Exception ex)
        {
            ExecutionResults = $"Error executing method: {ex.Message}";
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void ExecuteMethodWrapper(object? parameter)
    {
        ExecuteMethod();
    }

    private string FormatResults(WmiParameterCollection? results)
    {
        if (results == null || results.Count == 0)
        {
            return "Method executed successfully with no output parameters.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Method executed successfully. Output parameters:");

        foreach (var param in results)
        {
            sb.AppendLine($"  {param.Name}: {param.Value}");
        }

        return sb.ToString();
    }

    private void LoadMethodParameters()
    {
        _parameters.Clear();
        if (_method != null && _method.InParameters.Count > 0)
        {
            foreach (var param in _method.InParameters)
            {
                _parameters.Add(new WmiParameterViewModel(param));
            }
        }
    }
}

public class WmiParameterViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isDescriptionExpanded = false;
    private bool _isSelected = false;
    private object? _value;

    public WmiParameterViewModel(WmiParameter model)
    {
        Model = model;
        _value = model.Value;

        // Initialize commands
        ExpandDescriptionCommand = new RelayCommand(_ => IsDescriptionExpanded = true);
        CollapseDescriptionCommand = new RelayCommand(_ => IsDescriptionExpanded = false);
    }

    public ICommand CollapseDescriptionCommand { get; }
    public string? Description => Model.Description;
    public ICommand ExpandDescriptionCommand { get; }
    public int Id => Model.Id;
    public bool IsArray => Model.IsArray;
    public bool IsComplexType => IsComplex(Type);

    public bool IsDescriptionExpanded
    {
        get => _isDescriptionExpanded;
        set
        {
            if (_isDescriptionExpanded != value)
            {
                _isDescriptionExpanded = value;
                OnPropertyChanged(nameof(IsDescriptionExpanded));
            }
        }
    }

    public bool IsEnabled => IsSelected && !IsComplexType;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
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
            if (_value != value)
            {
                _value = value;
                Model.Value = value;
                OnPropertyChanged(nameof(Value));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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