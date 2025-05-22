using WmiExplorer.Common.Base;

namespace WmiExplorer.Presentation.ViewModels;

/// <summary>
/// Encapsulates all state, validation, and query construction for a WMI event query.
/// </summary>
public class WmiWatcherQueryBuilder : ViewModelBase
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

    private string? _additionalSelectFields;
    private string? _eventClass = "__InstanceCreationEvent";
    private string? _eventProperty = null;
    private string? _eventPropertyValue = "";
    private string? _eventQuery = "";
    private string? _eventTargetClass = "";

    // For validation, not used in query

    private string? _eventTargetClassProperty = null;

    private string? _eventTargetClassPropertyValue = "";
    private int _eventWithin = 5;
    private bool _isIntrinsicEvent = true;
    private string? _lastEventTargetClass = null;
    private string? _validationError = null;

    public string? AdditionalSelectFields
    {
        get => _additionalSelectFields;
        set
        {
            if (_additionalSelectFields != value)
            {
                _additionalSelectFields = value;
                OnPropertyChanged();
                BuildQuery();
            }
        }
    }

    public string? EventClass
    {
        get => _eventClass;
        set
        {
            if (SetProperty(ref _eventClass, value))
            {
                BuildQuery();
                OnPropertyChanged(nameof(IsEventPropertyValueEnabled));
                OnPropertyChanged(nameof(IsTargetClassEnabled));
                OnPropertyChanged(nameof(IsTargetClassPropertyEnabled));
                OnPropertyChanged(nameof(IsEventTargetClassPropertyValueEnabled));
                OnPropertyChanged(nameof(EventClass));

                // Clear EventPropertyValue if property value entry is now disabled
                if (!IsEventPropertyValueEnabled && !string.IsNullOrEmpty(EventPropertyValue))
                {
                    EventPropertyValue = string.Empty;
                }

                // Save and clear EventTargetClass and EventTargetClassProperty if selector is now disabled
                if (!IsTargetClassEnabled)
                {
                    if (!string.IsNullOrEmpty(EventTargetClass))
                    {
                        _lastEventTargetClass = EventTargetClass;
                        EventTargetClass = string.Empty;
                    }
                    if (!string.IsNullOrEmpty(EventTargetClassProperty))
                        EventTargetClassProperty = null;
                }
                // Restore EventTargetClass if selector is now enabled and we have a previous value
                else if (string.IsNullOrEmpty(EventTargetClass) && !string.IsNullOrEmpty(_lastEventTargetClass))
                {
                    EventTargetClass = _lastEventTargetClass;
                }
            }
        }
    }

    public string? EventProperty
    {
        get => _eventProperty;
        set
        {
            if (SetProperty(ref _eventProperty, value))
            {
                BuildQuery();
            }
        }
    }

    public string EventPropertyValue
    {
        get => _eventPropertyValue ?? string.Empty;
        set
        {
            if (SetProperty(ref _eventPropertyValue, value))
            {
                BuildQuery();
            }
        }
    }

    public string EventQuery
    {
        get => _eventQuery ?? string.Empty;
        set => SetProperty(ref _eventQuery, value);
    }

    public string? EventTargetClass
    {
        get => _eventTargetClass;
        set
        {
            if (SetProperty(ref _eventTargetClass, value))
            {
                BuildQuery();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected property of the EventTargetClass.
    /// </summary>
    public string? EventTargetClassProperty
    {
        get => _eventTargetClassProperty;
        set
        {
            if (SetProperty(ref _eventTargetClassProperty, value))
            {
                BuildQuery();
                OnPropertyChanged(nameof(IsEventTargetClassPropertyValueEnabled));
            }
        }
    }

    /// <summary>
    /// Gets or sets the value for the selected property of the EventTargetClass.
    /// </summary>
    public string EventTargetClassPropertyValue
    {
        get => _eventTargetClassPropertyValue ?? string.Empty;
        set
        {
            if (SetProperty(ref _eventTargetClassPropertyValue, value))
            {
                BuildQuery();
            }
        }
    }

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

    public int EventWithin
    {
        get => _eventWithin;
        set
        {
            if (SetProperty(ref _eventWithin, value > 0 ? value : 1))
            {
                BuildQuery();
            }
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
            // Disabled for instance events
            if (EventType == WmiEventType.Instance)
                return false;
            // Only compute, do not mutate state
            return EventType != WmiEventType.Namespace && EventType != WmiEventType.Class;
        }
    }

    /// <summary>
    /// True if the TargetClassProperty value entry should be enabled in the UI.
    /// </summary>
    public bool IsEventTargetClassPropertyValueEnabled
    {
        get
        {
            if (EventType == WmiEventType.Extrinsic)
                return false;
            if (!IsTargetClassPropertyEnabled)
                return false;
            return !string.IsNullOrWhiteSpace(EventTargetClassProperty);
        }
    }

    public bool IsIntrinsicEvent
    {
        get => _isIntrinsicEvent;
        set
        {
            if (SetProperty(ref _isIntrinsicEvent, value))
            {
                OnPropertyChanged(nameof(IsWithinEnabled));
                OnPropertyChanged(nameof(IsTargetClassEnabled));
                OnPropertyChanged(nameof(IsTargetClassPropertyEnabled));
                OnPropertyChanged(nameof(IsEventTargetClassPropertyValueEnabled));
                OnPropertyChanged(nameof(IsEventPropertyEnabled));
            }
        }
    }

    /// <summary>
    /// True if the TargetClass selector should be enabled in the UI.
    /// </summary>
    public bool IsTargetClassEnabled
    {
        get
        {
            // Disable for extrinsic events
            if (EventType == WmiEventType.Extrinsic)
                return false;
            if (EventType == WmiEventType.Instance || EventType == WmiEventType.Class)
                return true;
            if (EventType == WmiEventType.Namespace)
                return false;
            return true;
        }
    }

    /// <summary>
    /// True if the TargetClassProperty selector should be enabled in the UI.
    /// </summary>
    public bool IsTargetClassPropertyEnabled
    {
        get
        {
            if (EventType == WmiEventType.Extrinsic)
                return false;
            if (EventType == WmiEventType.Instance)
                return true;
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

    public string? ValidationError
    {
        get => _validationError;
        set => SetProperty(ref _validationError, value);
    }

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

        // Select fields
        string selectFields = string.IsNullOrWhiteSpace(AdditionalSelectFields) ? "*" : AdditionalSelectFields;

        // Start query
        string query = $"SELECT {selectFields} FROM {EventClass}";
        if (isIntrinsic)
        {
            query += $" WITHIN {EventWithin}";
        }

        // Determine event property
        string eventProp = string.IsNullOrWhiteSpace(EventProperty) ? GetDefaultEventProperty(EventClass) : EventProperty!;

        // WHERE clause construction
        string whereClause = string.Empty;
        bool hasTargetClass = !string.IsNullOrWhiteSpace(EventTargetClass);
        bool hasEventPropertyValue = !string.IsNullOrWhiteSpace(EventPropertyValue);
        bool hasTargetClassPropertyValue = !string.IsNullOrWhiteSpace(EventTargetClassPropertyValue);
        var whereParts = new List<string>();

        // Only add ISA filter for intrinsic events and if event property is set
        if (isIntrinsic && hasTargetClass && !string.IsNullOrWhiteSpace(eventProp))
        {
            whereParts.Add($"{eventProp} ISA '{EventTargetClass}'");
        }

        // Add property value condition for EventPropertyValue
        if (hasEventPropertyValue && IsEventPropertyValueEnabled)
        {
            if (IsTargetClassEnabled && !string.IsNullOrWhiteSpace(EventTargetClassProperty))
            {
                // Use EventProperty.EventTargetClassProperty = 'PropertyValue'
                whereParts.Add($"{eventProp}.{EventTargetClassProperty} = '{EventPropertyValue}'");
            }
            else if (!string.IsNullOrWhiteSpace(eventProp))
            {
                whereParts.Add($"{eventProp} = '{EventPropertyValue}'");
            }
        }

        // Add property value condition for EventTargetClassPropertyValue
        if (hasTargetClassPropertyValue && IsEventTargetClassPropertyValueEnabled)
        {
            if (!string.IsNullOrWhiteSpace(EventTargetClassProperty))
            {
                // Use EventProperty.EventTargetClassProperty = 'TargetClassPropertyValue'
                whereParts.Add($"{eventProp}.{EventTargetClassProperty} = '{EventTargetClassPropertyValue}'");
            }
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
}