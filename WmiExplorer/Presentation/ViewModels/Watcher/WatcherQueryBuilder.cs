using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Base;

namespace WmiExplorer.Presentation.ViewModels.Watcher;

/// <summary>
/// Encapsulates all state, validation, and query construction for a WMI event query.
/// </summary>
public partial class WatcherQueryBuilder : DisposableObservableObject
{
    /// <summary>
    /// Represents the type of WMI event for query building and UI logic.
    /// </summary>
    public enum WmiEventType
    {
        Unknown,
        Extrinsic,
        Instance,
        Class,
        Namespace,
        Method
    }

    [ObservableProperty]
    private string? _eventClass = string.Empty;

    [ObservableProperty]
    private PropertyDisplayInfo? _eventProperty = null;

    [ObservableProperty]
    private string? _eventPropertyValue = string.Empty;

    [ObservableProperty]
    private string? _eventQuery = string.Empty;

    [ObservableProperty]
    private string? _eventTargetClass = string.Empty;

    [ObservableProperty]
    private PropertyDisplayInfo? _eventTargetClassProperty = null;

    [ObservableProperty]
    private string? _eventTargetClassPropertyValue = string.Empty;

    [ObservableProperty]
    private int _eventWithin = 5;

    [ObservableProperty]
    private bool _isIntrinsicEvent = true;

    [ObservableProperty]
    private string? _validationError = null;

    /// <summary>
    /// Gets the event type based on the EventClass property.
    /// </summary>
    public WmiEventType EventType
    {
        get
        {
            if (string.IsNullOrWhiteSpace(EventClass))
                return WmiEventType.Unknown;
            if (!EventClass.StartsWith("__"))
                return WmiEventType.Extrinsic;
            if (EventClass.StartsWith("__Instance", StringComparison.OrdinalIgnoreCase))
                return WmiEventType.Instance;
            if (EventClass.StartsWith("__Class", StringComparison.OrdinalIgnoreCase))
                return WmiEventType.Class;
            if (EventClass.StartsWith("__Namespace", StringComparison.OrdinalIgnoreCase))
                return WmiEventType.Namespace;
            if (EventClass.StartsWith("__Method", StringComparison.OrdinalIgnoreCase))
                return WmiEventType.Method;
            return WmiEventType.Unknown;
        }
    }

    /// <summary>
    /// True if the EventProperty selector should be enabled in the UI.
    /// </summary>
    public bool IsEventPropertyEnabled => true;

    /// <summary>
    /// True if the property value entry should be enabled in the UI.
    /// </summary>
    public bool IsEventPropertyValueEnabled
    {
        get
        {
            // True by default, but false if EventProperty.Type is "object"
            return !(EventProperty != null && string.Equals(EventProperty.Type, "object", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// True if the TargetClassProperty value entry should be enabled in the UI.
    /// </summary>
    public bool IsEventTargetClassPropertyValueEnabled
    {
        get
        {
            // True if IsTargetClassPropertyEnabled, else false
            return IsTargetClassPropertyEnabled;
        }
    }

    /// <summary>
    /// True if the TargetClass selector should be enabled in the UI.
    /// </summary>
    public bool IsTargetClassEnabled
    {
        get
        {
            // True if EventProperty.Type == "object", else false
            return EventProperty != null && string.Equals(EventProperty.Type, "object", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// True if the TargetClassProperty selector should be enabled in the UI.
    /// </summary>
    public bool IsTargetClassPropertyEnabled
    {
        get
        {
            // True if IsTargetClassEnabled, except for Class and Namespace event types
            if (!IsTargetClassEnabled)
                return false;
            if (EventType == WmiEventType.Class || EventType == WmiEventType.Namespace)
                return false;
            return true;
        }
    }

    /// <summary>
    /// True if the polling interval (WITHIN) should be enabled in the UI.
    /// </summary>
    public bool IsWithinEnabled => IsIntrinsicEvent;

    // Optional: comma-separated fields

    public string? NamespaceContext { get; set; }

    /// <summary>
    /// Build the WMI event query string from the current builder state.
    /// </summary>
    public void BuildQuery()
    {
        ValidationError = null;
        EventQuery = string.Empty;

        // Validate event class
        if (string.IsNullOrWhiteSpace(EventClass))
        {
            ValidationError = "Event class is required.";
            return;
        }

        // Use new logic: intrinsic if EventType is not Extrinsic
        bool isIntrinsic = EventType != WmiEventType.Extrinsic;
        IsIntrinsicEvent = isIntrinsic;

        // Validate polling interval for intrinsic
        if (isIntrinsic && EventWithin <= 0)
        {
            ValidationError = "Polling interval (WITHIN) must be a positive integer for intrinsic events.";
            return;
        }

        // Start query
        string query = $"SELECT * FROM {EventClass}";
        if (isIntrinsic)
        {
            query += $" WITHIN {EventWithin}";
        }

        // Determine event property
        string eventProp = EventProperty?.Name ?? GetDefaultEventProperty(EventClass);

        // Use has* variables for clarity and safety
        bool hasTargetClass = IsTargetClassEnabled && !string.IsNullOrWhiteSpace(EventTargetClass);
        bool hasEventPropertyValue = IsEventPropertyValueEnabled && !string.IsNullOrWhiteSpace(EventPropertyValue);
        bool hasTargetClassPropertyValue = IsEventTargetClassPropertyValueEnabled &&
            !string.IsNullOrWhiteSpace(EventTargetClassPropertyValue) &&
            !string.IsNullOrWhiteSpace(EventTargetClass) &&
            !string.IsNullOrWhiteSpace(EventTargetClassProperty?.Name);

        // WHERE clause construction
        string whereClause = string.Empty;
        var whereParts = new List<string>();

        // Only add ISA filter for intrinsic events and if event property is set and TargetClass is enabled
        if (isIntrinsic && hasTargetClass && !string.IsNullOrWhiteSpace(eventProp))
        {
            whereParts.Add($"{eventProp} ISA '{EventTargetClass}'");
        }

        // Add property value condition for EventPropertyValue
        if (hasEventPropertyValue && !string.IsNullOrWhiteSpace(eventProp))
        {
            whereParts.Add($"{eventProp} = '{EventPropertyValue}'");
        }

        // Add property value condition for EventTargetClassPropertyValue
        if (hasTargetClassPropertyValue)
        {
            // EventTargetClassProperty is guaranteed not null here due to hasTargetClassPropertyValue
            whereParts.Add($"{eventProp}.{EventTargetClassProperty!.Name} = '{EventTargetClassPropertyValue}'");
        }

        if (whereParts.Count > 0)
        {
            whereClause = " WHERE " + string.Join(" AND ", whereParts);
        }

        query += whereClause;
        EventQuery = query;
        ValidationError = null;
    }

    /// <summary>
    /// Returns the default event property for the given event class.
    /// </summary>
    private string GetDefaultEventProperty(string? eventClass)
    {
        if (string.IsNullOrWhiteSpace(eventClass)) return string.Empty;
        if (eventClass.StartsWith("__Instance")) return "TargetInstance";
        if (eventClass.StartsWith("__Class")) return "TargetClass";
        if (eventClass.StartsWith("__Namespace")) return "TargetNamespace";
        if (eventClass.StartsWith("__Method")) return "Method";
        return ""; // For extrinsic or unknown
    }

    /// <summary>
    /// Raises property changed notifications for all computed UI state properties that depend on EventClass or EventProperty.
    /// </summary>
    private void NotifyEventUiStateProperties()
    {
        OnPropertyChanged(nameof(IsEventPropertyValueEnabled));
        OnPropertyChanged(nameof(IsTargetClassEnabled));
        OnPropertyChanged(nameof(IsTargetClassPropertyEnabled));
        OnPropertyChanged(nameof(IsEventTargetClassPropertyValueEnabled));
    }

    /// <summary>
    /// Called when EventClass property changes
    /// </summary>
    partial void OnEventClassChanged(string? value)
    {
        BuildQuery();
        NotifyEventUiStateProperties();

        // Clear EventPropertyValue if property value entry is now disabled
        if (!IsEventPropertyValueEnabled && !string.IsNullOrEmpty(EventPropertyValue))
        {
            EventPropertyValue = string.Empty;
        }
    }

    /// <summary>
    /// Called when EventProperty property changes
    /// </summary>
    partial void OnEventPropertyChanged(PropertyDisplayInfo? value)
    {
        NotifyEventUiStateProperties();
        BuildQuery();
    }

    /// <summary>
    /// Called when EventPropertyValue property changes
    /// </summary>
    partial void OnEventPropertyValueChanged(string? value)
    {
        BuildQuery();
    }

    /// <summary>
    /// Called when EventTargetClass property changes
    /// </summary>
    partial void OnEventTargetClassChanged(string? value)
    {
        BuildQuery();
    }

    /// <summary>
    /// Called when EventTargetClassProperty property changes
    /// </summary>
    partial void OnEventTargetClassPropertyChanged(PropertyDisplayInfo? value)
    {
        OnPropertyChanged(nameof(IsEventTargetClassPropertyValueEnabled));
        BuildQuery();
    }

    /// <summary>
    /// Called when EventTargetClassPropertyValue property changes
    /// </summary>
    partial void OnEventTargetClassPropertyValueChanged(string? value)
    {
        BuildQuery();
    }

    /// <summary>
    /// Called when EventWithin property changes
    /// </summary>
    partial void OnEventWithinChanged(int value)
    {
        BuildQuery();
    }

    /// <summary>
    /// Called when EventWithin property changes
    /// </summary>
    partial void OnEventWithinChanging(int value)
    {
        // Ensure value is positive
        if (value <= 0)
        {
            EventWithin = 1;
            return;
        }
    }

    /// <summary>
    /// Called when IsIntrinsicEvent property changes
    /// </summary>
    partial void OnIsIntrinsicEventChanged(bool value)
    {
        OnPropertyChanged(nameof(IsWithinEnabled));
        OnPropertyChanged(nameof(IsTargetClassEnabled));
        OnPropertyChanged(nameof(IsTargetClassPropertyEnabled));
        OnPropertyChanged(nameof(IsEventTargetClassPropertyValueEnabled));
        OnPropertyChanged(nameof(IsEventPropertyEnabled));
    }
}