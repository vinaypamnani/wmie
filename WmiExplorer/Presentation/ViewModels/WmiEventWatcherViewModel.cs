using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels
{
    /// <summary>
    /// View model for WMI Event Watcher tab
    /// </summary>
    public class WmiEventWatcherViewModel : MessagingViewModelBase
    {
        private readonly ObservableCollection<string> _targetClasses = new ObservableCollection<string>();
        private readonly ObservableCollection<string> _intrinsicEvents = new ObservableCollection<string>
        {
            "__InstanceCreationEvent",
            "__InstanceModificationEvent",
            "__InstanceDeletionEvent",
            "__InstanceOperationEvent",
            "__ClassCreationEvent",
            "__ClassModificationEvent",
            "__ClassDeletionEvent"
        };
        private readonly IMessagingService _messagingService;
        private readonly IWmiEventWatcherService _eventWatcherService;
        private readonly DebounceDispatcher _debouncer = new();
        private readonly ObservableCollection<WmiEventWatcherItemViewModel> _watchers = new();
        private readonly ObservableCollection<WmiEvent> _events = new();

        private string _eventType = "__InstanceCreationEvent";
        private int _within = 5;
        private string _targetClass = "";
        private string _condition = "";
        private string _eventQuery = "";
        private bool _isCustomQuery = false;
        private bool _canAddWatcher = false;
        private WmiNamespaceViewModel? _selectedNamespace;
        private ICommand? _addWatcherCommand;
        private string _classSearchText = string.Empty;
        private string _pendingClassSearch = string.Empty;
        private WmiEvent? _selectedEvent;
        private string? _selectedWatcherName;

        /// <summary>
        /// Initializes a new instance of the <see cref="WmiEventWatcherViewModel"/> class.
        /// </summary>
        /// <param name="messagingService">The messaging service to use</param>
        /// <param name="eventWatcherService">The WMI event watcher service to use</param>
        public WmiEventWatcherViewModel(IMessagingService messagingService, IWmiEventWatcherService eventWatcherService)
        {
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _eventWatcherService = eventWatcherService ?? throw new ArgumentNullException(nameof(eventWatcherService));

            InitializeMessaging(_messagingService);

            // Collection view for target classes
            TargetClasses = new ReadOnlyObservableCollection<string>(_targetClasses);
            TargetClassesView = CollectionViewSource.GetDefaultView(TargetClasses);
            TargetClassesView.Filter = ClassSearchFilter;

            // Collection for intrinsic events
            IntrinsicEvents = new ReadOnlyObservableCollection<string>(_intrinsicEvents);
            IntrinsicEventsView = CollectionViewSource.GetDefaultView(IntrinsicEvents);

            // Watchers and events
            Watchers = new ReadOnlyObservableCollection<WmiEventWatcherItemViewModel>(_watchers);
            Events = new ReadOnlyObservableCollection<WmiEvent>(_events);
            ClearEventsCommand = new RelayCommand(_ => ClearEvents());

            // Subscribe to namespace selection changes and class loading
            StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
            StrongSubscribe<ClassesLoadedMessage>(HandleClassesLoadedMessage);

            // Default event query
            UpdateEventQuery();

            // Initialize with empty target classes
            _targetClasses.Clear();
            TargetClassesView.Refresh();

            // Initialize with default event query disabled until namespace selected
            CanAddWatcher = false;

            // In the constructor, after initializing Watchers:
            ((INotifyCollectionChanged)_watchers).CollectionChanged += (s, e) => UpdateWatcherNames();
            UpdateWatcherNames();
        }

        /// <summary>
        /// Gets or sets the search text for filtering classes
        /// </summary>
        public string ClassSearchText
        {
            get => _pendingClassSearch;
            set
            {
                if (SetProperty(ref _pendingClassSearch, value))
                {
                    _debouncer.Debounce(() =>
                    {
                        if (_classSearchText != _pendingClassSearch)
                        {
                            _classSearchText = _pendingClassSearch;
                            TargetClassesView.Refresh();
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Gets a collection of target WMI classes available for event watching
        /// </summary>
        public ReadOnlyObservableCollection<string> TargetClasses { get; }

        /// <summary>
        /// Gets the collection view for target classes
        /// </summary>
        public ICollectionView TargetClassesView { get; }

        /// <summary>
        /// Gets a collection of intrinsic WMI event types
        /// </summary>
        public ReadOnlyObservableCollection<string> IntrinsicEvents { get; }

        /// <summary>
        /// Gets the collection view for intrinsic events
        /// </summary>
        public ICollectionView IntrinsicEventsView { get; }

        /// <summary>
        /// Gets or sets the selected namespace for event watching
        /// </summary>
        public WmiNamespaceViewModel? SelectedNamespace
        {
            get => _selectedNamespace;
            set
            {
                if (SetProperty(ref _selectedNamespace, value))
                {
                    // Clear target classes first
                    _targetClasses.Clear();
                    TargetClass = string.Empty;
                    TargetClassesView.Refresh();

                    // Update target classes based on selected namespace
                    if (value != null)
                    {
                        UpdateTargetClasses();
                    }

                    // Update ability to add watchers
                    CanAddWatcher = _selectedNamespace != null;

                    // Update the event query
                    UpdateEventQuery();
                }
            }
        }

        /// <summary>
        /// Gets or sets the event type for WMI events
        /// </summary>
        public string EventType
        {
            get => _eventType;
            set
            {
                if (SetProperty(ref _eventType, value))
                {
                    // Only update the query if we're not in custom mode
                    if (!IsCustomQuery)
                    {
                        UpdateEventQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the polling interval in seconds
        /// </summary>
        public int Within
        {
            get => _within;
            set
            {
                if (SetProperty(ref _within, value > 0 ? value : 1))
                {
                    // Only update the query if we're not in custom mode
                    if (!IsCustomQuery)
                    {
                        UpdateEventQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the target class for the event watcher
        /// </summary>
        public string TargetClass
        {
            get => _targetClass;
            set
            {
                if (SetProperty(ref _targetClass, value))
                {
                    // Only update the query if we're not in custom mode
                    if (!IsCustomQuery)
                    {
                        UpdateEventQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the condition for the event query
        /// </summary>
        public string Condition
        {
            get => _condition;
            set
            {
                if (SetProperty(ref _condition, value))
                {
                    // Only update the query if we're not in custom mode
                    if (!IsCustomQuery)
                    {
                        UpdateEventQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the WMI event query
        /// </summary>
        public string EventQuery
        {
            get => _eventQuery;
            set => SetProperty(ref _eventQuery, value);
        }

        /// <summary>
        /// Gets or sets whether the query is a custom query
        /// </summary>
        public bool IsCustomQuery
        {
            get => _isCustomQuery;
            set
            {
                if (SetProperty(ref _isCustomQuery, value))
                {
                    OnPropertyChanged(nameof(IsQueryReadOnly));

                    // If switching from custom to auto, update the query
                    if (!value)
                    {
                        UpdateEventQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Gets whether the query text box is read-only
        /// </summary>
        public bool IsQueryReadOnly => !_isCustomQuery;

        /// <summary>
        /// Gets or sets whether a watcher can be added
        /// </summary>
        public bool CanAddWatcher
        {
            get => _canAddWatcher;
            set => SetProperty(ref _canAddWatcher, value);
        }

        /// <summary>
        /// Gets the command to add a new watcher
        /// </summary>
        public ICommand AddWatcherCommand
        {
            get
            {
                return _addWatcherCommand ??= new RelayCommand(
                    _ => AddWatcher(),
                    _ => CanAddWatcher);
            }
        }

        /// <summary>
        /// Gets the collection of watchers
        /// </summary>
        public ReadOnlyObservableCollection<WmiEventWatcherItemViewModel> Watchers { get; }

        /// <summary>
        /// Gets the collection of events
        /// </summary>
        public ReadOnlyObservableCollection<WmiEvent> Events { get; }

        /// <summary>
        /// Gets the command to clear events
        /// </summary>
        public ICommand ClearEventsCommand { get; }

        /// <summary>
        /// Gets or sets the selected event
        /// </summary>
        public WmiEvent? SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (SetProperty(ref _selectedEvent, value))
                {
                    _messagingService.Publish(new SelectedEventChangedMessage(value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected watcher name
        /// </summary>
        public string? SelectedWatcherName
        {
            get => _selectedWatcherName;
            set
            {
                if (SetProperty(ref _selectedWatcherName, value))
                {
                    EventsView.Refresh();
                }
            }
        }

        /// <summary>
        /// Gets a collection of watcher names from the existing watchers
        /// </summary>
        public ObservableCollection<string> WatcherNames { get; } = new();

        private ICollectionView? _eventsView;

        /// <summary>
        /// Gets the collection view for events, filtered by the selected watcher name
        /// </summary>
        public ICollectionView EventsView
        {
            get
            {
                if (_eventsView == null)
                {
                    _eventsView = CollectionViewSource.GetDefaultView(Events);
                    _eventsView.Filter = FilterByWatcherName;
                }
                return _eventsView;
            }
        }

        /// <summary>
        /// Filter predicate for events by watcher name
        /// </summary>
        private bool FilterByWatcherName(object obj)
        {
            if (obj is not WmiEvent evt)
                return false;
            if (string.IsNullOrEmpty(SelectedWatcherName))
                return true;
            return evt.WatcherName == SelectedWatcherName;
        }

        /// <summary>
        /// Handles when the selected namespace changes
        /// </summary>
        private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
        {
            if (message?.NamespaceViewModel == null)
                return;

            SelectedNamespace = message.NamespaceViewModel;
        }

        /// <summary>
        /// Handles when classes are loaded in a namespace
        /// </summary>
        private void HandleClassesLoadedMessage(ClassesLoadedMessage message)
        {
            if (message?.NamespaceViewModel == null)
                return;

            // Only update if this is our selected namespace
            if (_selectedNamespace == message.NamespaceViewModel)
            {
                UpdateTargetClasses();
            }
        }

        /// <summary>
        /// Updates the event query based on the current settings
        /// </summary>
        private void UpdateEventQuery()
        {
            // Build the WMI event query
            string query = $"SELECT * FROM {_eventType} WITHIN {_within}";

            // Add WHERE clause with target class if one is selected
            if (!string.IsNullOrEmpty(_targetClass))
            {
                query += $" WHERE TargetInstance ISA '{_targetClass}'";

                // Add condition if provided
                if (!string.IsNullOrWhiteSpace(_condition))
                {
                    query += $" AND {_condition}";
                }
            }

            EventQuery = query;

            // Update CanAddWatcher property based on whether we have a valid query
            CanAddWatcher = _selectedNamespace != null && !string.IsNullOrWhiteSpace(EventQuery);
        }

        /// <summary>
        /// Adds a new watcher based on the current event query
        /// </summary>
        private void AddWatcher()
        {
            if (_selectedNamespace == null)
            {
                PublishErrorState("No namespace selected.");
                return;
            }

            try
            {
                var watcher = new WmiEventWatcher(
                    EventQuery,
                    _selectedNamespace.ManagementScope,
                    _eventWatcherService);
                var watcherViewModel = new WmiEventWatcherItemViewModel(
                    watcher,
                    _messagingService,
                    RemoveWatcher,
                    OnEventReceived);
                _watchers.Add(watcherViewModel);
                watcher.Start();
                PublishSuccessState($"Added watcher: {watcher.Name}");
            }
            catch (Exception ex)
            {
                PublishErrorState($"Failed to add watcher: {ex.Message}", ex);
            }
        }

        private void RemoveWatcher(WmiEventWatcherItemViewModel watcher)
        {
            if (_watchers.Remove(watcher))
            {
                watcher.Dispose();
                PublishSuccessState($"Removed watcher: {watcher.Name}");
            }
        }

        private void OnEventReceived(WmiEvent wmiEvent)
        {
            RunOnUIThread(() =>
            {
                _events.Add(wmiEvent);
                const int maxEvents = 1000;
                while (_events.Count > maxEvents)
                    _events.RemoveAt(0);
            });
        }

        public void ClearEvents() => _events.Clear();

        /// <summary>
        /// Updates the target classes collection based on the selected namespace
        /// </summary>
        private void UpdateTargetClasses()
        {
            if (_selectedNamespace == null)
            {
                _targetClasses.Clear();
                TargetClass = string.Empty;
                CanAddWatcher = false;
                TargetClassesView.Refresh();
                return;
            }

            // Clear existing classes
            _targetClasses.Clear();

            // Add classes from the namespace and sort them
            var sortedClasses = _selectedNamespace.Classes
                .OrderBy(c => c.ClassName)
                .Select(c => c.ClassName);

            foreach (var className in sortedClasses)
            {
                _targetClasses.Add(className);
            }

            // Check if current target class still exists
            if (!string.IsNullOrEmpty(_targetClass) && !_targetClasses.Contains(_targetClass))
            {
                TargetClass = string.Empty;
            }

            // Refresh the view
            TargetClassesView.Refresh();

            // Update the event query
            UpdateEventQuery();
        }

        /// <summary>
        /// Filter predicate for class search
        /// </summary>
        private bool ClassSearchFilter(object item)
        {
            if (string.IsNullOrEmpty(_classSearchText))
                return true;

            if (item is string className)
                return className.Contains(_classSearchText, StringComparison.OrdinalIgnoreCase);

            return false;
        }

        /// <summary>
        /// ViewModel for a single WMI event watcher item
        /// </summary>
        public class WmiEventWatcherItemViewModel : ViewModelBase, IDisposable
        {
            private readonly WmiEventWatcher _watcher;
            private readonly IMessagingService _messagingService;
            private readonly Action<WmiEventWatcherItemViewModel> _onRemove;
            private readonly Action<WmiEvent> _onEventReceived;
            private bool _disposed;

            /// <summary>
            /// Gets the name of the watcher
            /// </summary>
            public string Name => _watcher.Name;

            /// <summary>
            /// Gets the WQL query used by this watcher
            /// </summary>
            public string Query => _watcher.Query;

            /// <summary>
            /// Gets the namespace path this watcher is monitoring
            /// </summary>
            public string Namespace => _watcher.Namespace;

            /// <summary>
            /// Gets whether the watcher is currently running
            /// </summary>
            public bool IsRunning => _watcher.IsRunning;

            /// <summary>
            /// Gets when this watcher was created
            /// </summary>
            public DateTime CreatedAt => _watcher.CreatedAt;

            /// <summary>
            /// Gets the command to start the watcher
            /// </summary>
            public ICommand StartCommand { get; }

            /// <summary>
            /// Gets the command to stop the watcher
            /// </summary>
            public ICommand StopCommand { get; }

            /// <summary>
            /// Gets the command to remove the watcher
            /// </summary>
            public ICommand RemoveCommand { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="WmiEventWatcherItemViewModel"/> class.
            /// </summary>
            public WmiEventWatcherItemViewModel(
                WmiEventWatcher watcher,
                IMessagingService messagingService,
                Action<WmiEventWatcherItemViewModel> onRemove,
                Action<WmiEvent> onEventReceived)
            {
                _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
                _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
                _onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
                _onEventReceived = onEventReceived ?? throw new ArgumentNullException(nameof(onEventReceived));

                StartCommand = new RelayCommand(_ => Start(), _ => !IsRunning);
                StopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
                RemoveCommand = new RelayCommand(_ => Remove());

                _watcher.EventArrived += OnEventArrived;
            }

            private void Start()
            {
                try
                {
                    _watcher.Start();
                    OnPropertyChanged(nameof(IsRunning));
                }
                catch (Exception ex)
                {
                    _messagingService.Publish(new ApplicationStateMessage(ApplicationState.Error($"Failed to start watcher: {ex.Message}", ex)));
                }
            }

            private void Stop()
            {
                try
                {
                    _watcher.Stop();
                    OnPropertyChanged(nameof(IsRunning));
                }
                catch (Exception ex)
                {
                    _messagingService.Publish(new ApplicationStateMessage(ApplicationState.Error($"Failed to stop watcher: {ex.Message}", ex)));
                }
            }

            private void Remove()
            {
                _onRemove(this);
            }

            private void OnEventArrived(object? sender, ManagementBaseObject e)
            {
                var wmiEvent = new WmiEvent(Name, e);
                _onEventReceived(wmiEvent);
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _watcher.EventArrived -= OnEventArrived;
                    _watcher.Dispose();
                    _disposed = true;
                }
            }
        }

        private void UpdateWatcherNames()
        {
            var names = _watchers.Select(w => w.Name).Distinct().OrderBy(n => n).ToList();
            WatcherNames.Clear();
            foreach (var name in names)
                WatcherNames.Add(name);
        }
    }
}
