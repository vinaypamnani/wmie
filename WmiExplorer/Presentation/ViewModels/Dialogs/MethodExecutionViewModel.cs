using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Dialogs;

/// <summary>
/// ViewModel for executing WMI methods (both static and instance)
/// </summary>
public partial class MethodExecutionDialogViewModel : DisposableObservableObject
{
    public event EventHandler? CloseRequested;

    private readonly WmiClass? _class;
    private CancellationTokenSource _cts = new();

    [ObservableProperty]
    private AppState _executionState = AppState.Ready;

    [ObservableProperty]
    private bool _hasOutputParameters;

    private readonly WmiInstance? _instance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExecutionState))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteMethodCommand))]
    private bool _isExecuting;

    private ManagementScope _managementScope;
    private readonly WmiMethod? _method;
    private readonly WmiNamespace? _namespace;

    [ObservableProperty]
    private WmiBaseObject? _outputParameters;

    private readonly ObservableCollection<WmiParameterViewModel> _parameters = new();

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    private readonly IWmiService _wmiService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodExecutionDialogViewModel"/> class.
    /// </summary>    /// <param name="wmiNamespace">The WMI namespace containing the class.</param>
    /// <param name="wmiClass">The WMI class containing the method.</param>
    /// <param name="wmiMethod">The WMI method to execute.</param>
    /// <param name="wmiInstance">The WMI instance for non-static methods (optional).</param>
    /// <param name="wmiService">The WMI service for executing methods.</param>
    public MethodExecutionDialogViewModel(
        IWmiService wmiService,
        WmiNamespace wmiNamespace,
        WmiClass wmiClass,
        WmiMethod wmiMethod,
        WmiInstance? wmiInstance = null)
    {
        _namespace = wmiNamespace ?? throw new ArgumentNullException(nameof(wmiNamespace));
        _class = wmiClass ?? throw new ArgumentNullException(nameof(wmiClass));
        _method = wmiMethod ?? throw new ArgumentNullException(nameof(wmiMethod));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _instance = wmiInstance;

        // Set management scope based on instance or class
        if (_instance != null && _instance.ActualObject != null)
        {
            _managementScope = _instance.ActualObject.Scope;
        }
        else if (_class != null && _class.ActualClass != null)
        {
            _managementScope = _class.ActualClass.Scope;
        }
        else
        {
            throw new ArgumentException("Could not determine management scope from class or instance");
        }

        if (_instance == null && !_method.IsStatic)
        {
            throw new ArgumentException("Cannot execute non-static method without an instance");
        }

        LoadMethodParameters();
    }

    /// <summary>
    /// Gets the class name.
    /// </summary>
    public string ClassName => _class!.ClassName;

    /// <summary>
    /// Gets the instance name (if applicable).
    /// </summary>
    public string InstanceName => _instance?.InstanceName ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the method is static.
    /// </summary>
    public bool IsStaticMethod => _method!.IsStatic;

    /// <summary>
    /// Gets the description of the method being executed.
    /// </summary>
    public string MethodDescription => _method!.Description;    /// <summary>
    /// Gets the name of the method being executed.
    /// </summary>
    public string MethodName => _method!.Name;    /// <summary>
    /// Gets the URL for learning more about this method.
    /// </summary>
    public string LearnMoreUrl => $"http://www.bing.com/search?q={MethodName}+Method+of+the+{ClassName}+Class";

    /// <summary>
    /// Gets the display text for the learn more hyperlink.
    /// </summary>
    public string LearnMoreText => $"Learn more about {MethodName} method of the {ClassName} class";

    /// <summary>
    /// Gets the collection of parameters for the method.
    /// </summary>
    public ObservableCollection<WmiParameterViewModel> Parameters => _parameters;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Command to cancel the current operation
    /// </summary>
    [RelayCommand(CanExecute = nameof(CancelCanExecute))]
    private void Cancel()
    {
        try
        {
            // Cancel any ongoing method execution
            _cts?.Cancel();
            StatusMessage = "Requesting cancellation...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error cancelling operation: {ex.Message}";
        }
    }

    /// <summary>
    /// Determines if the cancel command can be executed
    /// </summary>
    private bool CancelCanExecute() => IsExecuting;

    /// <summary>
    /// Command to close the dialog
    /// </summary>
    [RelayCommand]
    private void CloseDialog()
    {
        // Cancel any ongoing operation before closing
        if (IsExecuting)
        {
            try
            {
                _cts?.Cancel();
                StatusMessage = "Cancelling operation and closing...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error cancelling operation: {ex.Message}";
            }
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Command to execute the method
    /// </summary>
    [RelayCommand(CanExecute = nameof(ExecuteMethodCanExecute))]
    private async Task ExecuteMethodAsync()
    {
        // Create a new cancellation token source for this execution
        // This is necessary because once a CTS is cancelled, it cannot be reused
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            // Set executing state
            IsExecuting = true;
            ExecutionState = AppState.Busy;
            StatusMessage = $"Executing method {MethodName}...";

            // Reset output parameters
            HasOutputParameters = false;
            OutputParameters = null;

            // Reset output parameters
            HasOutputParameters = false;
            OutputParameters = null;

            // Create parameters for the method invocation
            ManagementBaseObject? inParams = null;

            if (_parameters.Count > 0)
            {
                // Get the in-parameters for the method from the class
                inParams = _class!.ActualClass.GetMethodParameters(_method!.Name);

                // Set parameter values
                foreach (var param in _parameters.Where(p => p.IsSelected))
                {
                    if (param.Name != null && param.Value != null)
                    {
                        inParams[param.Name] = param.Value;
                    }
                }
            }

            ManagementBaseObject? outParams;

            if (_instance != null)
            {
                // Execute instance method
                outParams = await _wmiService.ExecuteMethodAsync(
                    _instance.ActualObject,
                    _method!.Name,
                    inParams,
                    _cts.Token);
            }
            else
            {
                // Execute static method
                outParams = await _wmiService.ExecuteStaticMethodAsync(
                    _class!.ActualClass,
                    _method!.Name,
                    inParams,
                    _cts.Token);
            }

            // Update output parameters
            if (outParams != null && outParams.Properties.Count > 0)
            {
                OutputParameters = new WmiBaseObject(outParams);
                HasOutputParameters = true;
            }

            // Update status
            ExecutionState = AppState.Success;
            StatusMessage = "Method executed successfully";

            // Switch to Output tab to show results
            SelectedTabIndex = 1;
        }
        catch (OperationCanceledException)
        {
            ExecutionState = AppState.Warning;
            StatusMessage = "Method execution was cancelled";
        }
        catch (Exception ex)
        {
            ExecutionState = AppState.Error;
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            // Always clear executing state
            IsExecuting = false;
        }
    }

    /// <summary>
    /// Determines if the execute method command can be executed
    /// </summary>
    private bool ExecuteMethodCanExecute() => !IsExecuting;

    private void LoadMethodParameters()
    {
        _parameters.Clear();
        if (_method != null && _method.InParameters.Count > 0)
        {
            foreach (var param in _method.InParameters.OrderBy(p => p.Id))
            {
                var parameterViewModel = new WmiParameterViewModel(param, _wmiService, _managementScope);
                if (parameterViewModel.IsReference)
                {
                    parameterViewModel.PropertyChanged += WmiParameterViewModel_PropertyChanged;
                }
                _parameters.Add(parameterViewModel);
            }
        }
    }

    partial void OnIsExecutingChanged(bool value)
    {
        if (value)
        {
            ExecutionState = AppState.Busy;
            StatusMessage = "Executing method...";
        }
    }

    private void WmiParameterViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WmiParameterViewModel.ReferenceLoadState))
        {
            var param = (WmiParameterViewModel)sender!;

            // Update status based on reference load state
            switch (param.ReferenceLoadState)
            {
                case ReferenceValueLoadState.Loading:
                    StatusMessage = "Loading reference values...";
                    ExecutionState = AppState.Busy;
                    break;
                case ReferenceValueLoadState.Loaded:
                    StatusMessage = "Reference values loaded.";
                    ExecutionState = AppState.Success;
                    break;
                case ReferenceValueLoadState.Cancelled:
                    StatusMessage = "Reference value loading cancelled.";
                    ExecutionState = AppState.Warning;
                    break;
                case ReferenceValueLoadState.Error:
                    StatusMessage = "Error loading reference values.";
                    ExecutionState = AppState.Error;
                    break;
                default:
                    break;
            }
        }
    }
}