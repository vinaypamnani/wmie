using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection.Emit;
using System.Windows.Data;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels;

public class PropertyDisplayInfo
{
    public string Display => string.IsNullOrEmpty(Type) ? Name : $"{Name} [{Type}]";
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    public override string ToString() => Display;
}

/// <summary>
/// View model for WMI Event Watcher tab
/// </summary>
public class WmiWatcherViewModel : MessagingViewModelBase
{
    private ICommand? _addWatcherCommand;
    private readonly ICacheService _cacheService;
    private bool _canAddWatcher = false;
    private readonly DebounceDispatcher _debouncer = new();
    private readonly ObservableCollection<string> _eventClassList = new ObservableCollection<string>();
    private PropertyDisplayInfo _eventDisplayProperty = new PropertyDisplayInfo { Name = "__RELPATH", Type = "string" };
    private readonly ObservableCollection<PropertyDisplayInfo> _eventDisplayPropertyList = new ObservableCollection<PropertyDisplayInfo>();
    private readonly ObservableCollection<PropertyDisplayInfo> _eventPropertyList = new ObservableCollection<PropertyDisplayInfo>();
    private readonly WmiWatcherQueryBuilder _eventQueryBuilder;
    private readonly ObservableCollection<WmiEvent> _events = new();
    private ICollectionView? _eventsView;
    private readonly ObservableCollection<string> _eventTargetClassList = new ObservableCollection<string>();
    private readonly ObservableCollection<PropertyDisplayInfo> _eventTargetClassPropertyList = new ObservableCollection<PropertyDisplayInfo>();
    private bool _isCustomQuery = false;
    private readonly IMessagingService _messagingService;
    private string _pendingEventTargetClassSearch = string.Empty;
    private WmiEvent? _selectedEvent;
    private WmiNamespaceViewModel? _selectedNamespace;
    private string? _selectedWatcherName;
    private readonly ObservableCollection<WmiWatcherItem> _watchers = new();
    private int _watcherId = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="WmiWatcherViewModel"/> class.
    /// </summary>
    /// <param name="messagingService">The messaging service to use</param>
    /// <param name="cacheService">The cache service to use</param>
    public WmiWatcherViewModel(
        IMessagingService messagingService,
        ICacheService cacheService
    )
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        _eventQueryBuilder = new WmiWatcherQueryBuilder();

        InitializeMessaging(_messagingService);

        // Collection view for target classes
        EventTargetClassList = new ReadOnlyObservableCollection<string>(_eventTargetClassList);
        EventTargetClassListView = CollectionViewSource.GetDefaultView(EventTargetClassList);
        EventTargetClassListView.Filter = ClassSearchFilter;

        EventClassList = new ReadOnlyObservableCollection<string>(_eventClassList);
        EventClassListView = CollectionViewSource.GetDefaultView(EventClassList);

        // Watchers and events
        Watchers = new ReadOnlyObservableCollection<WmiWatcherItem>(_watchers);
        Events = new ReadOnlyObservableCollection<WmiEvent>(_events);
        ClearEventsCommand = new RelayCommand(_ => ClearEvents());

        // Subscribe to namespace selection changes and class loading
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
        StrongSubscribe<ClassesLoadedMessage>(HandleClassesLoadedMessage);

        // Initialize with empty target classes
        _eventTargetClassList.Clear();
        EventTargetClassListView.Refresh();

        // Initialize with default event query disabled until namespace selected
        CanAddWatcher = false;

        // In the constructor, after initializing Watchers:
        ((INotifyCollectionChanged)_watchers).CollectionChanged += (s, e) =>
            UpdateWatcherNames();

        UpdateWatcherNames();

        StartAllCommand = new RelayCommand(_ => StartAllWatchers(), _ => _watchers.Count > 0);
        StopAllCommand = new RelayCommand(_ => StopAllWatchers(), _ => _watchers.Count > 0);
        RemoveAllCommand = new RelayCommand(_ => RemoveAllWatchers(), _ => _watchers.Count > 0);

        // Collection for event properties
        EventPropertyList = new ReadOnlyObservableCollection<PropertyDisplayInfo>(_eventPropertyList);
        // New: ReadOnly collection for EventTargetClassPropertyList
        EventTargetClassPropertyList = new ReadOnlyObservableCollection<PropertyDisplayInfo>(_eventTargetClassPropertyList);
        // New: ReadOnly collection for EventDisplayPropertyList
        EventDisplayPropertyList = new ReadOnlyObservableCollection<PropertyDisplayInfo>(_eventDisplayPropertyList);

        // Subscribe to builder property changes for UI sync and CanAddWatcher
        _eventQueryBuilder.PropertyChanged += EventQueryBuilder_PropertyChanged;
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
    /// Gets the command to clear events
    /// </summary>
    public ICommand ClearEventsCommand { get; }

    // Change private set to get-only for read-only properties
    public ReadOnlyObservableCollection<string> EventClassList { get; }

    public ICollectionView EventClassListView { get; }

    /// <summary>
    /// Gets or sets the property name to use for event display
    /// </summary>
    public PropertyDisplayInfo EventDisplayProperty
    {
        get => _eventDisplayProperty;
        set
        {
            if (SetProperty(ref _eventDisplayProperty, value))
            {
                // Optionally, update watchers or events if needed
            }
        }
    }

    public ReadOnlyObservableCollection<PropertyDisplayInfo> EventDisplayPropertyList { get; }

    /// <summary>
    /// Gets a collection of intrinsic WMI event properties for the selected event class
    /// </summary>
    public ReadOnlyObservableCollection<PropertyDisplayInfo> EventPropertyList { get; }

    /// <summary>
    /// Gets or sets the WMI event query
    /// </summary>
    public string EventQuery
    {
        get => EventQueryBuilder.EventQuery;
        set
        {
            EventQueryBuilder.EventQuery = value;
        }
    }

    public WmiWatcherQueryBuilder EventQueryBuilder => _eventQueryBuilder;

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

    public ReadOnlyObservableCollection<string> EventTargetClassList { get; }
    public ICollectionView EventTargetClassListView { get; }
    public ReadOnlyObservableCollection<PropertyDisplayInfo> EventTargetClassPropertyList { get; }

    /// <summary>
    /// Gets or sets the search text for filtering classes
    /// </summary>
    public string EventTargetClassSearchText
    {
        get => _pendingEventTargetClassSearch;
        set
        {
            if (SetProperty(ref _pendingEventTargetClassSearch, value))
            {
                _debouncer.Debounce(() =>
                {
                    EventTargetClassListView.Refresh();
                });
            }
        }
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
            }
        }
    }

    /// <summary>
    /// Gets whether the query text box is read-only
    /// </summary>
    public bool IsQueryReadOnly => !_isCustomQuery;

    /// <summary>
    /// Gets the command to remove all watchers
    /// </summary>
    public ICommand RemoveAllCommand { get; }

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
                EventQueryBuilder.EventTargetClass = string.Empty;
                if (value != null)
                {
                    _ = UpdateEventTargetClassListAsync();
                }
                CanAddWatcher = _selectedNamespace != null && !string.IsNullOrWhiteSpace(EventQueryBuilder.EventQuery);
                UpdateEventClassList();
                UpdateEventPropertyList();
                UpdateEventTargetClassPropertyList(); // Also update target class properties when namespace changes
                UpdateEventDisplayPropertyList(); // Ensure display property list is updated when namespace changes
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
    /// Gets the command to start all watchers
    /// </summary>
    public ICommand StartAllCommand { get; }

    /// <summary>
    /// Gets the command to stop all watchers
    /// </summary>
    public ICommand StopAllCommand { get; }

    /// <summary>
    /// Gets a collection of watcher names from the existing watchers
    /// </summary>
    public ObservableCollection<string> WatcherNames { get; } = new();

    /// <summary>
    /// Gets the collection of watchers
    /// </summary>
    public ReadOnlyObservableCollection<WmiWatcherItem> Watchers { get; }

    public void ClearEvents() => _events.Clear();

    /// <summary>
    /// Adds a new watcher based on the current event query
    /// </summary>
    private void AddWatcher()
    {
        if (SelectedNamespace == null)
        {
            PublishErrorState("No namespace selected.");
            return;
        }

        try
        {
            // Compose watcher name as <EventType>_<TargetClass>_<Id>
            string watcherName = string.IsNullOrWhiteSpace(EventQueryBuilder.EventClass) ? "Unknown" : EventQueryBuilder.EventClass;
            if (!string.IsNullOrWhiteSpace(EventQueryBuilder.EventTargetClass))
                watcherName += "_" + EventQueryBuilder.EventTargetClass;
            watcherName += $"_{_watcherId}";

            var watcher = new WmiEventWatcher(
                watcherName,
                EventQueryBuilder.EventQuery,
                SelectedNamespace.ManagementScope);

            // Start the watcher before adding to the collection
            watcher.Start();

            var watcherViewModel = new WmiWatcherItem(
                watcher,
                RemoveWatcher,
                OnEventReceived,
                EventQueryBuilder.EventClass ?? "Unknown",
                EventDisplayProperty.Name
            );
            _watchers.Add(watcherViewModel);
            _watcherId++; // Increment for next watcher
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
        if (string.IsNullOrEmpty(_pendingEventTargetClassSearch))
            return true;

        if (item is string className)
            return className.Contains(_pendingEventTargetClassSearch, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private void EventQueryBuilder_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WmiWatcherQueryBuilder.EventClass))
        {
            UpdateEventPropertyList();
            UpdateEventTargetClassPropertyList(); // Also update property list when EventClass changes
            UpdateEventDisplayPropertyList(); // Ensure display property list is updated when event class changes
        }
        if (e.PropertyName == nameof(WmiWatcherQueryBuilder.EventTargetClass))
        {
            UpdateEventTargetClassPropertyList();
            UpdateEventDisplayPropertyList();
        }
        if (e.PropertyName == nameof(WmiWatcherQueryBuilder.EventQuery))
        {
            // Update CanAddWatcher whenever the query changes
            CanAddWatcher = SelectedNamespace != null && !string.IsNullOrWhiteSpace(EventQueryBuilder.EventQuery);
        }
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
    /// Helper method to retrieve WMI class properties from either in-memory classes or cache
    /// </summary>
    private async Task GetAndPopulateWmiClassPropertiesAsync(
    string? className,
    ObservableCollection<PropertyDisplayInfo> targetCollection)
    {
        targetCollection.Clear();
        IEnumerable<PropertyDisplayInfo> propertyInfos = Enumerable.Empty<PropertyDisplayInfo>();

        if (_selectedNamespace != null && !string.IsNullOrEmpty(className))
        {
            // Prefer in-memory class properties if available
            var inMemoryClass = _selectedNamespace.Classes?.FirstOrDefault(c => c.ClassName == className);
            if (inMemoryClass != null && inMemoryClass.WmiClass != null && inMemoryClass.WmiClass.Properties != null && inMemoryClass.WmiClass.Properties.Count > 0)
            {
                propertyInfos = inMemoryClass.WmiClass.Properties
                    .Cast<System.Management.PropertyData>()
                    .Select(p => new PropertyDisplayInfo
                    {
                        Name = p.Name,
                        Type = p.Type.ToString() != null ? p.Type.ToString() : string.Empty
                    });
            }
            else
            {
                try
                {
                    var nsCache = await _cacheService.GetNamespaceCacheAsync(_selectedNamespace.NamespacePath);
                    if (nsCache != null && nsCache.Classes != null && nsCache.Classes.Count > 0)
                    {
                        var cachedClass = nsCache.Classes.FirstOrDefault(c => c.ClassName == className);
                        if (cachedClass != null && cachedClass.Properties != null && cachedClass.Properties.Count > 0)
                        {
                            propertyInfos = cachedClass.Properties.Select(p => new PropertyDisplayInfo
                            {
                                Name = p.Name,
                                Type = p.Type ?? string.Empty
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cache error: {ex.Message}");
                }
            }
        }

        // Exclude TIME_CREATED and SECURITY_DESCRIPTOR
        propertyInfos = propertyInfos.Where(p =>
            !string.Equals(p.Name, "TIME_CREATED", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(p.Name, "SECURITY_DESCRIPTOR", StringComparison.OrdinalIgnoreCase));

        foreach (var prop in propertyInfos)
            targetCollection.Add(prop);
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
            _ = UpdateEventTargetClassListAsync();
            UpdateEventClassList();
            UpdateEventPropertyList();
            UpdateEventTargetClassPropertyList(); // Also update target class properties when classes are loaded
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

    /// <summary>
    /// Helper to populate class lists for event or target classes
    /// </summary>
    private async Task PopulateClassListAsync(bool eventClassesOnly, ObservableCollection<string> targetCollection, ICollectionView viewToRefresh)
    {
        targetCollection.Clear();
        IEnumerable<string> classNames = Enumerable.Empty<string>();

        if (_selectedNamespace != null)
        {
            // Prefer in-memory classes if available
            var inMemory = _selectedNamespace.Classes?
                .Where(c => eventClassesOnly ? c.IsEventClass : !c.IsEventClass)
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
                    var nsCache = await _cacheService.GetNamespaceCacheAsync(_selectedNamespace.NamespacePath);
                    if (nsCache != null && nsCache.Classes != null && nsCache.Classes.Count > 0)
                    {
                        classNames = nsCache.Classes
                            .Where(c => eventClassesOnly ? c.IsEventClass : !c.IsEventClass)
                            .Select(c => c.ClassName);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cache error: {ex.Message}");
                }
            }
        }

        if (eventClassesOnly && !classNames.Any())
        {
            // Fallback to default event class list
            classNames = new[]
            {
                "__InstanceCreationEvent",
                "__InstanceModificationEvent",
                "__InstanceDeletionEvent",
                "__InstanceOperationEvent",
                "__ClassCreationEvent",
                "__ClassModificationEvent",
                "__ClassDeletionEvent",
            };
        }

        // Sort: system classes (names starting with "__") first, then others, both groups sorted ascending (A-Z)
        var systemClasses = classNames.Where(n => n.StartsWith("__")).Distinct().OrderBy(n => n, StringComparer.Ordinal);
        var userClasses = classNames.Where(n => !n.StartsWith("__")).Distinct().OrderBy(n => n, StringComparer.Ordinal);
        foreach (var name in systemClasses.Concat(userClasses))
            targetCollection.Add(name);

        viewToRefresh.Refresh();
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

    private void RemoveWatcher(WmiWatcherItem watcher)
    {
        if (_watchers.Remove(watcher))
        {
            watcher.Dispose();
            PublishSuccessState($"Removed watcher: {watcher.Name}");
        }
    }

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

    private async void UpdateEventClassList()
    {
        await PopulateClassListAsync(true, _eventClassList, EventClassListView);
    }

    private void UpdateEventDisplayPropertyList()
    {
        _eventDisplayPropertyList.Clear();
        if (EventQueryBuilder.IsTargetClassEnabled && EventQueryBuilder.IsTargetClassPropertyEnabled && EventQueryBuilder.EventType != WmiWatcherQueryBuilder.WmiEventType.Method)
        {
            foreach (var prop in EventTargetClassPropertyList)
            {
                _eventDisplayPropertyList.Add(prop);
            }
        }
        else if (!EventQueryBuilder.IsIntrinsicEvent)
        {
            foreach (var prop in EventPropertyList)
            {
                _eventDisplayPropertyList.Add(prop);
            }
        }
        else
        {
            _eventDisplayPropertyList.Add(new PropertyDisplayInfo
            {
                Name = "__RELPATH",
                Type = "string"
            });
        }

        // Set default if needed
        EventDisplayProperty = _eventDisplayPropertyList.FirstOrDefault() ?? new PropertyDisplayInfo { Name = "__RELPATH", Type = "string" };
    }

    private async void UpdateEventPropertyList()
    {
        await GetAndPopulateWmiClassPropertiesAsync(EventQueryBuilder.EventClass, _eventPropertyList);
        if (_eventPropertyList.Count > 0)
        {
            // Look for properties starting with "Target"
            PropertyDisplayInfo? targetProp = null;
            targetProp = _eventPropertyList
                .FirstOrDefault(p => p.Name.StartsWith("Target", StringComparison.OrdinalIgnoreCase));

            // If no target property found, use the first property
            if (targetProp == null && _eventPropertyList.Count > 0)
            {
                targetProp = _eventPropertyList[0];
            }

            // Set the property - We're using the actual object reference from _eventPropertyList
            EventQueryBuilder.EventProperty = targetProp;

            // Update related UI elements
            UpdateEventDisplayPropertyList();
        }
    }

    private async Task UpdateEventTargetClassListAsync()
    {
        await PopulateClassListAsync(false, _eventTargetClassList, EventTargetClassListView);

        // Check if current target class still exists
        if (!string.IsNullOrEmpty(EventQueryBuilder.EventTargetClass) && !_eventTargetClassList.Contains(EventQueryBuilder.EventTargetClass))
        {
            EventQueryBuilder.EventTargetClass = string.Empty;
        }
    }

    private async void UpdateEventTargetClassPropertyList()
    {
        await GetAndPopulateWmiClassPropertiesAsync(EventQueryBuilder.EventTargetClass, _eventTargetClassPropertyList);
        if (_eventTargetClassPropertyList.Count > 0)
        {
            // Find the preferred property in the actual _eventTargetClassPropertyList collection
            PropertyDisplayInfo? preferredPropInList = null;

            // Look for identifying properties first
            var preferredPropName = _eventTargetClassPropertyList
                .FirstOrDefault(p =>
                    p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.EndsWith("Name", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains("Display", StringComparison.OrdinalIgnoreCase))?.Name;

            if (preferredPropName != null)
            {
                // Find this property in the actual _eventTargetClassPropertyList (to get the correct object reference)
                preferredPropInList = _eventTargetClassPropertyList.FirstOrDefault(p =>
                    p.Name.Equals(preferredPropName, StringComparison.OrdinalIgnoreCase));
            }

            // If no preferred property found, use the first property
            if (preferredPropInList == null && _eventTargetClassPropertyList.Count > 0)
            {
                preferredPropInList = _eventTargetClassPropertyList[0];
            }

            // Set the property using the actual object reference from _eventTargetClassPropertyList
            EventQueryBuilder.EventTargetClassProperty = preferredPropInList;

            // Update related UI elements
            UpdateEventDisplayPropertyList();
        }
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
        {
            if (_selectedWatcherName != "All")
                SelectedWatcherName = "All";
        }
    }
}