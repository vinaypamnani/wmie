using WmiExplorer.Common.Base;

namespace WmiExplorer.Presentation.ViewModels
{
    /// <summary>
    /// Encapsulates all state, validation, and query construction for a WMI event query.
    /// </summary>
    public class WmiWatcherQueryBuilder : ViewModelBase
    {
        private string? _eventClass = "__InstanceCreationEvent";
        private int _eventWithin = 5;
        private string? _eventProperty = null;
        private string? _eventTargetClass = "";
        private string? _eventCondition = "";
        private string? _eventQuery = "";
        private string? _validationError = null;
        private bool _isIntrinsicEvent = true;
        private bool _isCustomQuery = false;

        public string? EventClass
        {
            get => _eventClass;
            set
            {
                if (SetProperty(ref _eventClass, value))
                {
                    BuildQuery();
                    OnPropertyChanged(nameof(IsTargetClassEnabled));
                }
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
        public string EventCondition
        {
            get => _eventCondition ?? string.Empty;
            set
            {
                if (SetProperty(ref _eventCondition, value))
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
        public string? ValidationError
        {
            get => _validationError;
            set => SetProperty(ref _validationError, value);
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
                    OnPropertyChanged(nameof(IsEventPropertyEnabled));
                }
            }
        }
        public bool IsCustomQuery
        {
            get => _isCustomQuery;
            set => SetProperty(ref _isCustomQuery, value);
        }

        public string? AdditionalSelectFields {
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
        private string? _additionalSelectFields; // Optional: comma-separated fields

        public string? NamespaceContext { get; set; } // For validation, not used in query

        /// <summary>
        /// Determines if the event class is intrinsic (starts with '__').
        /// </summary>
        private bool IsIntrinsic(string? eventClass)
        {
            return !string.IsNullOrWhiteSpace(eventClass) && eventClass.StartsWith("__");
        }

        /// <summary>
        /// Returns the default event property for the given event class.
        /// </summary>
        private string GetDefaultEventProperty(string? eventClass)
        {
            if (string.IsNullOrWhiteSpace(eventClass)) return "TargetInstance";
            if (eventClass.StartsWith("__Instance")) return "TargetInstance";
            if (eventClass.StartsWith("__Class")) return "TargetClass";
            if (eventClass.StartsWith("__Namespace")) return "TargetNamespace";
            return ""; // For extrinsic or unknown
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

            bool isIntrinsic = IsIntrinsic(EventClass);
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
            bool hasCondition = !string.IsNullOrWhiteSpace(EventCondition);
            var whereParts = new List<string>();

            // Only add ISA filter for intrinsic events and if event property is set
            if (isIntrinsic && hasTargetClass && !string.IsNullOrWhiteSpace(eventProp))
            {
                whereParts.Add($"{eventProp} ISA '{EventTargetClass}'");
            }
            // For extrinsic events, do not add ISA filter

            if (hasCondition)
            {
                whereParts.Add(EventCondition!);
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
        /// True if the polling interval (WITHIN) should be enabled in the UI.
        /// </summary>
        public bool IsWithinEnabled => IsIntrinsicEvent;

        /// <summary>
        /// True if the TargetClass selector should be enabled in the UI.
        /// </summary>
        public bool IsTargetClassEnabled
        {
            get
            {
                // Disable for extrinsic and for any __Namespace* events (case-insensitive)
                if (!IsIntrinsicEvent) return false;
                if (!string.IsNullOrWhiteSpace(EventClass) && EventClass.StartsWith("__Namespace", StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }
        }

        /// <summary>
        /// True if the EventProperty selector should be enabled in the UI (intrinsic events only).
        /// </summary>
        public bool IsEventPropertyEnabled => IsIntrinsicEvent;
    }
}
