using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.ViewModels;

/// <summary>
/// ViewModel for executing WMI methods (both static and instance)
/// </summary>
public partial class MethodExecutionViewModel : DisposableObservableObject
{
    public event EventHandler? CloseRequested;

    private readonly WmiClass _class;

    [ObservableProperty]
    private bool _hasOutputParameters;

    private readonly WmiInstance? _instance;
    private readonly WmiMethod _method;
    private readonly WmiNamespace _namespace;

    [ObservableProperty]
    private WmiBaseObject? _outputParameters;

    private readonly ObservableCollection<MethodParameterViewModel> _parameters = new();

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _statusMessage = "Ready";

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
        }

        // Load parameters
        LoadMethodParameters();
    }

    /// <summary>
    /// Gets the class name.
    /// </summary>
    public string ClassName => _class.ClassName;

    /// <summary>
    /// Gets the instance name (if applicable).
    /// </summary>
    public string InstanceName => _instance?.InstanceName ?? string.Empty;

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
    /// Gets the collection of parameters for the method.
    /// </summary>
    public ObservableCollection<MethodParameterViewModel> Parameters => _parameters;

    /// <summary>
    /// Command to cancel and close the dialog
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Command to execute the method
    /// </summary>
    [RelayCommand]
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
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void LoadMethodParameters()
    {
        _parameters.Clear();
        if (_method != null && _method.InParameters.Count > 0)
        {
            // Sort InParameters by Id before adding to _parameters
            foreach (var param in _method.InParameters.OrderBy(p => p.Id))
            {
                _parameters.Add(new MethodParameterViewModel(param));
            }
        }
    }
}