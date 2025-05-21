using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
        private readonly ICacheService _cacheService;
        private readonly DebounceDispatcher _debouncer = new();
        private readonly ObservableCollection<WmiEvent> _events = new();
        private readonly ObservableCollection<string> _intrinsicEvents = new ObservableCollection<string>();
        private readonly IMessagingService _messagingService;
        private readonly ObservableCollection<string> _targetClasses = new ObservableCollection<string>();
        private readonly ObservableCollection<WmiEventWatcherItemViewModel> _watchers = new();
        private ICommand? _addWatcherCommand;
        private bool _canAddWatcher = false;
        private string _classSearchText = string.Empty;
        private string _condition = "";
        private string _eventQuery = "";
        private ICollectionView? _eventsView;
        private string _eventType = "__InstanceCreationEvent";
        private bool _isCustomQuery = false;
        private string _pendingClassSearch = string.Empty;
        private WmiEvent? _selectedEvent;
        private WmiNamespaceViewModel? _selectedNamespace;
        private string? _selectedWatcherName;
        private string _targetClass = "";
        private int _within = 5;
        private string _eventDisplayPropertyName = "__RELPATH";

        /// <summary>
        /// Initializes a new instance of the <see cref="WmiEventWatcherViewModel"/> class.
        /// </summary>
        /// <param name="messagingService">The messaging service to use</param>
        /// <param name="cacheService">The cache service to use</param>
        public WmiEventWatcherViewModel(
            IMessagingService messagingService,
            ICacheService cacheService
        )
        {
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

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
            ((INotifyCollectionChanged)_watchers).CollectionChanged += (s, e) =>
                UpdateWatcherNames();

            UpdateWatcherNames();

            StartAllCommand = new RelayCommand(_ => StartAllWatchers(), _ => _watchers.Count > 0);
            StopAllCommand = new RelayCommand(_ => StopAllWatchers(), _ => _watchers.Count > 0);
            RemoveAllCommand = new RelayCommand(_ => RemoveAllWatchers(), _ => _watchers.Count > 0);
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
                    _ => CanAddWatcher
                );
            }
        }

        /// <summary>
        /// Gets or sets whether a watcher can be added
        /// </summary>
        public bool CanAddWatcher
        {
            get => _canAddWatcher;
            set => SetProperty(ref _canAddWatcher, value);
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
        /// Gets the command to clear events
        /// </summary>
        public ICommand ClearEventsCommand { get; }

        /// <summary>
        /// Gets the command to start all watchers
        /// </summary>
        public ICommand StartAllCommand { get; }

        /// <summary>
        /// Gets the command to stop all watchers
        /// </summary>
        public ICommand StopAllCommand { get; }

        /// <summary>
        /// Gets the command to remove all watchers
        /// </summary>
        public ICommand RemoveAllCommand { get; }

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
        /// Gets the collection of events
        /// </summary>
        public ReadOnlyObservableCollection<WmiEvent> Events { get; }

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
        /// Gets a collection of intrinsic WMI event types
        /// </summary>
        public ReadOnlyObservableCollection<string> IntrinsicEvents { get; }

        /// <summary>
        /// Gets the collection view for intrinsic events
        /// </summary>
        public ICollectionView IntrinsicEventsView { get; }

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

                    // Update intrinsic events
                    UpdateIntrinsicEvents();
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
        /// Gets a collection of target WMI classes available for event watching
        /// </summary>
        public ReadOnlyObservableCollection<string> TargetClasses { get; }

        /// <summary>
        /// Gets the collection view for target classes
        /// </summary>
        public ICollectionView TargetClassesView { get; }

        /// <summary>
        /// Gets a collection of watcher names from the existing watchers
        /// </summary>
        public ObservableCollection<string> WatcherNames { get; } = new();

        /// <summary>
        /// Gets the collection of watchers
        /// </summary>
        public ReadOnlyObservableCollection<WmiEventWatcherItemViewModel> Watchers { get; }

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
        /// Gets or sets the property name to use for event display
        /// </summary>
        public string EventDisplayPropertyName
        {
            get => _eventDisplayPropertyName;
            set
            {
                if (SetProperty(ref _eventDisplayPropertyName, value))
                {
                    // Optionally, update watchers or events if needed
                }
            }
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
                // Compose watcher name as <EventType>_<TargetClass>
                string watcherName = string.IsNullOrWhiteSpace(EventType) ? "Unknown" : EventType;
                if (!string.IsNullOrWhiteSpace(TargetClass))
                    watcherName += "_" + TargetClass;

                var watcher = new WmiEventWatcher(
                    watcherName,
                    EventQuery,
                    _selectedNamespace.ManagementScope);

                var watcherViewModel = new WmiEventWatcherItemViewModel(
                    watcher,
                    RemoveWatcher,
                    OnEventReceived,
                    EventType, // Pass the event type explicitly
                    EventDisplayPropertyName // Pass the display property name
                );
                _watchers.Add(watcherViewModel);
                watcher.Start();
                PublishSuccessState($"Added watcher: {watcher.Name}");
            }
            catch (Exception ex)
            {
                PublishErrorState($"Failed to add watcher: {ex.Message}", ex);
            }
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

        private bool FilterByWatcherName(object obj)
        {
            if (obj is not WmiEvent evt)
                return false;
            // If "All" is selected, show all events
            if (string.IsNullOrEmpty(SelectedWatcherName) || SelectedWatcherName == "All")
                return true;
            return evt.WatcherName == SelectedWatcherName;
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
                UpdateIntrinsicEvents();
            }
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

        private void RemoveWatcher(WmiEventWatcherItemViewModel watcher)
        {
            if (_watchers.Remove(watcher))
            {
                watcher.Dispose();
                PublishSuccessState($"Removed watcher: {watcher.Name}");
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

        private async void UpdateIntrinsicEvents()
        {
            _intrinsicEvents.Clear();
            var eventClassNames = Enumerable.Empty<string>();

            if (_selectedNamespace != null)
            {
                // Prefer in-memory classes if available
                var inMemory = _selectedNamespace
                    .Classes?.Where(c => c.IsEventClass)
                    .Select(c => c.ClassName)
                    .ToList();
                if (inMemory != null && inMemory.Count > 0)
                {
                    eventClassNames = inMemory;
                }
                else
                {
                    try
                    {
                        // Use cache service directly
                        var nsCache = await _cacheService.GetNamespaceCacheAsync(
                            _selectedNamespace.NamespacePath
                        );
                        if (nsCache != null && nsCache.Classes != null && nsCache.Classes.Count > 0)
                        {
                            eventClassNames = nsCache
                                .Classes.Where(c => c.IsEventClass)
                                .Select(c => c.ClassName);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log or handle cache retrieval errors gracefully
                        System.Diagnostics.Debug.WriteLine($"Cache error: {ex.Message}");
                    }
                }
            }

            if (!eventClassNames.Any())
            {
                // Fallback to default list
                eventClassNames =
                [
                    "__InstanceCreationEvent",
                    "__InstanceModificationEvent",
                    "__InstanceDeletionEvent",
                    "__InstanceOperationEvent",
                    "__ClassCreationEvent",
                    "__ClassModificationEvent",
                    "__ClassDeletionEvent",
                ];
            }

            // Sort event class names: system classes (names starting with "__") first, then others, both groups sorted ascending (A-Z)
            var systemEvents = eventClassNames
                .Where(n => n.StartsWith("__"))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal);
            var userEvents = eventClassNames
                .Where(n => !n.StartsWith("__"))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal);
            foreach (var name in systemEvents.Concat(userEvents))
                _intrinsicEvents.Add(name);

            IntrinsicEventsView.Refresh();
        }

        /// <summary>
        /// Updates the target classes collection based on the selected namespace
        /// </summary>
        private void UpdateTargetClasses()
        {
            _targetClasses.Clear();
            IEnumerable<string> classNames = Enumerable.Empty<string>();

            if (_selectedNamespace != null)
            {
                // Prefer in-memory classes if available
                var inMemory = _selectedNamespace
                    .Classes?.Where(c => !c.IsEventClass)
                    .Select(c => c.ClassName)
                    .ToList();
                if (inMemory != null && inMemory.Count > 0)
                {
                    classNames = inMemory;
                }
                else
                {
                    try
                    {
                        var nsCacheTask = _cacheService.GetNamespaceCacheAsync(
                            _selectedNamespace.NamespacePath
                        );
                        nsCacheTask.Wait(); // Synchronous wait since this is not async
                        var nsCache = nsCacheTask.Result;
                        if (nsCache != null && nsCache.Classes != null && nsCache.Classes.Count > 0)
                        {
                            classNames = nsCache
                                .Classes.Where(c => !c.IsEventClass)
                                .Select(c => c.ClassName);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cache error: {ex.Message}");
                    }
                }
            }

            // Add classes from the namespace and sort them
            foreach (var className in classNames.OrderBy(n => n, StringComparer.Ordinal))
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

        private void UpdateWatcherNames()
        {
            var names = _watchers.Select(w => w.Name).Distinct().OrderBy(n => n).ToList();
            WatcherNames.Clear();
            WatcherNames.Add("All"); // Always add "All" as the first item
            foreach (var name in names)
                WatcherNames.Add(name);
            // If the selected watcher name is not in the list, reset to "All"
            if (string.IsNullOrEmpty(_selectedWatcherName) || !WatcherNames.Contains(_selectedWatcherName))
                SelectedWatcherName = "All";
        }

        public void ClearEvents() => _events.Clear();

        private void StartAllWatchers()
        {
            foreach (var watcher in _watchers)
            {
                // Use the public StartCommand to ensure correct state and error handling
                if (watcher.StartCommand.CanExecute(null))
                    watcher.StartCommand.Execute(null);
            }
            PublishSuccessState("Started all watchers.");
        }

        private void StopAllWatchers()
        {
            foreach (var watcher in _watchers)
            {
                if (watcher.StopCommand.CanExecute(null))
                    watcher.StopCommand.Execute(null);
            }
            PublishSuccessState("Stopped all watchers.");
        }

        private void RemoveAllWatchers()
        {
            // Copy to list to avoid modifying collection during enumeration
            var toRemove = _watchers.ToList();
            foreach (var watcher in toRemove)
            {
                RemoveWatcher(watcher);
            }
            PublishSuccessState("Removed all watchers.");
        }
    }
}