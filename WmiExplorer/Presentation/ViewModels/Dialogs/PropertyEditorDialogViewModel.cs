using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Management;
using System.Windows;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.PropertyGrid.Editors.Core;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the PropertyEditorDialog that allows editing WMI objects.
/// </summary>
public partial class PropertyEditorDialogViewModel : MessagingViewModelBase
{
    [ObservableProperty]
    private AppState _appState = AppState.Ready;

    private readonly ManagementBaseObject? _clonedObject;
    private Dictionary<string, string> _currentErrorProperties = new();
    private HashSet<string> _currentModifiedProperties = new();
    private readonly int _dialogId;

    [ObservableProperty]
    private object? _editableObject;

    private int _lastErrorCount = 0;
    private int _lastModifiedCount = 0;

    [ObservableProperty]
    private string _objectTypeName = string.Empty;

    private readonly ManagementBaseObject? _originalObject;
    private readonly Dictionary<string, ReferenceValueLoadState> _referenceStates = new();
    private string _statusMessage = string.Empty;
    private string _statusTooltip = string.Empty;

    [ObservableProperty]
    private string _title = "Edit Properties";

    private readonly Window _window;

    /// <summary>
    /// Initializes the dialog for editing a raw ManagementBaseObject (instance editing).
    /// </summary>
    public PropertyEditorDialogViewModel(Window window, ManagementBaseObject managementObject, IMessengerService messengerService, string? title, int dialogId)
        : base(messengerService)
    {
        _dialogId = dialogId;
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _originalObject = managementObject ?? throw new ArgumentNullException(nameof(managementObject));

        Title = title ?? _title;
        ObjectTypeName = _originalObject.ClassPath?.ClassName ?? "Unknown";

        // Create a clone of the object for editing so changes don't affect the original until OK is clicked
        _clonedObject = (ManagementBaseObject)_originalObject.Clone();
        EditableObject = _clonedObject;

        StrongSubscribe<ReferenceLoadStateChangedMessage>(OnReferenceLoadStateChanged);

        // Subscribe to validation errors for proactive status updates
        ValidationManager.ValidationStateChanged += OnValidationStateChanged;
        UpdateStatusBar();
    }

    public bool IsAnyReferenceLoading => _referenceStates.Values.Any(state => state == ReferenceValueLoadState.Loading);

    /// <summary>
    /// Gets the cleaned result object. Only available after OK is clicked.
    /// </summary>
    public ManagementBaseObject? Result { get; private set; }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusTooltip
    {
        get => _statusTooltip;
        set => SetProperty(ref _statusTooltip, value);
    }

    // Unsubscribe to avoid memory leaks
    ~PropertyEditorDialogViewModel()
    {
        ValidationManager.ValidationStateChanged -= OnValidationStateChanged;
    }

    [RelayCommand]
    private void Cancel()
    {
        // Optionally set status before closing
        StatusMessage = "Edit cancelled.";
        AppState = AppState.Indeterminate;
        Result = null;
        _window.DialogResult = false;
        _window.Close();
    }

    /// <summary>
    /// Copies property values from the cloned object back to the original object.
    /// </summary>
    private void CopyPropertiesFromCloneToOriginal()
    {
        if (_originalObject == null || _clonedObject == null)
            return;

        var copyErrors = new List<string>();
        try
        {
            foreach (PropertyData property in _clonedObject.Properties)
            {
                try
                {
                    // Only copy writable properties
                    if (!property.IsLocal || property.Value == null)
                        continue;

                    // Set the property value on the original object
                    _originalObject.Properties[property.Name].Value = property.Value;
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to copy property '{property.Name}': {ex.Message}");
                    copyErrors.Add($"{property.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error copying properties from clone to original object");
            throw; // Re-throw since this is a critical operation
        }

        if (copyErrors.Count > 0)
        {
            StatusMessage = $"Some properties could not be loaded: {string.Join(", ", copyErrors)}. See the log for details.";
            AppState = AppState.Warning;
        }
    }

    // Replace FindValidationErrors with this method
    private List<string> GetCurrentValidationErrors()
    {
        return _currentErrorProperties.Select(kvp => $"• {kvp.Key}: {kvp.Value}").ToList();
    }

    [RelayCommand]
    private void Ok()
    {
        try
        {
            // Use the new error tracking instead of FindValidationErrors
            var validationErrors = GetCurrentValidationErrors();
            if (validationErrors.Count > 0)
            {
                StatusMessage = "Validation errors found. Please review highlighted fields.";
                AppState = AppState.Warning;
                MessageBox.Show(StatusTooltip, "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Don't close the dialog
            }

            // Copy changes from clone back to original and return the original (which is connected to WMI)
            if (_originalObject != null && _clonedObject != null)
            {
                CopyPropertiesFromCloneToOriginal();
                Result = _originalObject;
            }

            StatusMessage = "Properties saved successfully.";
            AppState = AppState.Success;
            _window.DialogResult = true;
            _window.Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing properties in PropertyEditorDialog");
            StatusMessage = $"Error processing properties: {ex.Message}";
            AppState = AppState.Error;
            MessageBox.Show($"Error processing properties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnReferenceLoadStateChanged(ReferenceLoadStateChangedMessage msg)
    {
        _referenceStates[msg.PropertyName] = msg.State;
        OnPropertyChanged(nameof(IsAnyReferenceLoading));
        OnPropertyChanged(nameof(StatusMessage));

        if (msg.State == ReferenceValueLoadState.Loaded)
        {
            StatusMessage = "Reference values loaded successfully.";
            AppState = AppState.Success;
        }
        else if (msg.State == ReferenceValueLoadState.Error)
        {
            StatusMessage = "Error loading reference values. Check the log for details.";
            AppState = AppState.Error;
        }
        else if (msg.State == ReferenceValueLoadState.Loading)
        {
            StatusMessage = "Loading reference values...";
            AppState = AppState.Busy;
        }
    }

    private void OnValidationStateChanged(object? sender, ValidationManager.ValidationStateChangedEventArgs e)
    {
        if (e.DialogId != _dialogId)
            return;
        _lastErrorCount = e.ErrorCount;
        _lastModifiedCount = e.ModifiedCount;
        _currentErrorProperties = new Dictionary<string, string>(e.ErrorProperties);
        _currentModifiedProperties = new HashSet<string>(e.ModifiedProperties);
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        if (_lastErrorCount > 0)
        {
            StatusMessage = $"{_lastModifiedCount} properties modified, {_lastErrorCount} with validation errors";
            AppState = AppState.Warning;
            // Set tooltip to detailed error message
            var validationErrors = GetCurrentValidationErrors();
            StatusTooltip =
                "Please fix the following validation errors before continuing:\n\n" +
                string.Join("\n", validationErrors) +
                "\n\nTip: Press Escape in any error field to reset to original value.";
        }
        else if (_lastModifiedCount > 0)
        {
            StatusMessage = $"{_lastModifiedCount} properties modified";
            AppState = AppState.Success;
            StatusTooltip = $"{_lastModifiedCount} properties modified. Click Save to save changes.";
        }
        else
        {
            StatusMessage = "Ready.";
            AppState = AppState.Ready;
            StatusTooltip = "Edit properties and click Save to save changes.";
        }
    }
}