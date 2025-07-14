using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Management;
using System.Windows;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.Views.Dialogs;
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
    private readonly bool _saveBeforeReturn;
    private string _statusMessage = string.Empty;
    private string _statusTooltip = string.Empty;

    [ObservableProperty]
    private string _title = "Edit Properties";

    private readonly Window _window;
    private readonly IWmiService? _wmiService;

    /// <summary>
    /// Initializes the dialog for editing a raw ManagementBaseObject (instance editing).
    /// </summary>
    public PropertyEditorDialogViewModel(Window window, ManagementBaseObject managementObject, IMessengerService messengerService, string? title, int dialogId, IWmiService? wmiService = null, bool saveBeforeReturn = false)
        : base(messengerService)
    {
        _dialogId = dialogId;
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _originalObject = managementObject ?? throw new ArgumentNullException(nameof(managementObject));
        _wmiService = wmiService;
        _saveBeforeReturn = saveBeforeReturn;
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
                    if (!property.IsLocal)
                        continue;

                    // Set the property value on the original object, including nulls
                    _originalObject.Properties[property.Name].Value = property.Value;
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to copy property to original object after edit: '{property.Name}': {ex.Message}");
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

    [RelayCommand]
    private async Task Save()
    {
        var objectPath = "Unknown";
        if (_originalObject is ManagementObject mo && mo.Path != null)
        {
            objectPath = mo.Path.Path;
        }
        else if (_originalObject is ManagementBaseObject mbo && mbo.ClassPath != null)
        {
            objectPath = mbo.ClassPath.Path;
        }

        try
        {
            // Use the new error tracking instead of FindValidationErrors
            var validationErrors = GetCurrentValidationErrors();
            if (validationErrors.Count > 0)
            {
                StatusMessage = "Validation errors found. Please review highlighted fields.";
                AppState = AppState.Warning;
                MessageBoxDialog.Show(StatusTooltip, "Validation Errors", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Warning, _window);
                return; // Don't close the dialog
            }

            // Copy changes from clone back to original and return the original (which is connected to WMI)
            if (_originalObject != null && _clonedObject != null)
            {
                CopyPropertiesFromCloneToOriginal();
                // Save if requested and object is ManagementObject
                if (_saveBeforeReturn && _wmiService != null && _originalObject is ManagementObject mgmtObj)
                {
                    Log.Debug("Saving instance before return: {Path}", objectPath!);
                    throw new InvalidOperationException("Save operation completed successfully, but dialog should not close yet.");
                    await _wmiService.SaveInstanceAsync(mgmtObj);
                }
                Result = _originalObject;
            }

            // StatusMessage = $"Instance {objectPath} saved successfully.";
            // AppState = AppState.Success;
            _window.DialogResult = true;
            _window.Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving instance: {Path}", objectPath);
            StatusMessage = $"Error saving instance {objectPath}: {ex.Message}";
            AppState = AppState.Error;
            MessageBoxDialog.Show(
                $"Failed to save {objectPath}:\n\n{ex.Message}",
                "Error",
                MessageBoxDialogButton.OK,
                MessageBoxDialogIcon.Error,
                _window);
        }
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
                "\n\nTip: Press Ctrl+Z in any field to reset to original value.";
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