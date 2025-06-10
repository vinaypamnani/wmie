using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Watcher;

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
public partial class WatcherTabViewModel : MessagingViewModelBase
{
    private readonly ICacheService _cacheService;

    [ObservableProperty]
    private bool _canAddWatcher = false;

    private readonly string _defaultEventclass = "__InstanceCreationEvent";
    private readonly ObservableCollection<string> _eventClassList = new ObservableCollection<string>();

    [ObservableProperty]
    private PropertyDisplayInfo _eventDisplayProperty = new PropertyDisplayInfo { Name = "__RELPATH", Type = "string" };

    private readonly ObservableCollection<PropertyDisplayInfo> _eventDisplayPropertyList = new ObservableCollection<PropertyDisplayInfo>();
    private readonly DebounceDispatcher _eventFilterDebouncer = new();

    [ObservableProperty]
    private string _eventFilterText = string.Empty;

    private readonly ObservableCollection<PropertyDisplayInfo> _eventPropertyList = new ObservableCollection<PropertyDisplayInfo>();
    private readonly WatcherQueryBuilder _eventQueryBuilder;
    private readonly ObservableCollection<WmiEvent> _events = new();
    private ICollectionView? _eventsView;
    private readonly FilterHelper<string> _eventTargetClassFilterHelper;
    private readonly ObservableCollection<string> _eventTargetClassList = new ObservableCollection<string>();
    private readonly ObservableCollection<PropertyDisplayInfo> _eventTargetClassPropertyList = new ObservableCollection<PropertyDisplayInfo>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQueryReadOnly))]
    private bool _isCustomQuery = false;

    [ObservableProperty]
    private WmiEvent? _selectedEvent;

    [ObservableProperty]
    private WmiNamespaceViewModel? _selectedNamespace;

    [ObservableProperty]
    private string? _selectedWatcherName;

    private int _watcherId = 1;
    private readonly ObservableCollection<WmiEventWatcherViewModel> _watchers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WatcherTabViewModel"/> class.
    /// </summary>
    /// <param name="messengerService">The messenger service to use</param>
    /// <param name="cacheService">The cache service to use</param>
    public WatcherTabViewModel(
        IMessengerService messengerService,
        ICacheService cacheService
    ) : base(messengerService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        _eventQueryBuilder = new WatcherQueryBuilder();
        _eventTargetClassFilterHelper = new FilterHelper<string>(
            _eventTargetClassList,
            (className, filter) => string.IsNullOrEmpty(filter) ||
                className.Contains(filter, StringComparison.OrdinalIgnoreCase)
        );

        // Collection view for target classes
        EventTargetClassList = new ReadOnlyObservableCollection<string>(_eventTargetClassList);
        EventTargetClassListView = _eventTargetClassFilterHelper.CollectionView;

        EventClassList = new ReadOnlyObservableCollection<string>(_eventClassList);
        EventClassListView = CollectionViewSource.GetDefaultView(EventClassList);

        // Watchers and events
        Watchers = new ReadOnlyObservableCollection<WmiEventWatcherViewModel>(_watchers);
        Events = new ReadOnlyObservableCollection<WmiEvent>(_events);

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

        // Collection for event properties
        EventPropertyList = new ReadOnlyObservableCollection<PropertyDisplayInfo>(_eventPropertyList);
        // New: ReadOnly collection for EventTargetClassPropertyList
        EventTargetClassPropertyList = new ReadOnlyObservableCollection<PropertyDisplayInfo>(_eventTargetClassPropertyList);
        // New: ReadOnly collection for EventDisplayPropertyList
        EventDisplayPropertyList = new ReadOnlyObservableCollection<PropertyDisplayInfo>(_eventDisplayPropertyList);

        // Subscribe to builder property changes for UI sync and CanAddWatcher
        _eventQueryBuilder.PropertyChanged += EventQueryBuilder_PropertyChanged;
    }

    // Change private set to get-only for read-only properties
    public ReadOnlyObservableCollection<string> EventClassList { get; }

    public ICollectionView EventClassListView { get; }
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
        get => EventQueryBuilder.EventQuery ?? string.Empty;
        set
        {
            EventQueryBuilder.EventQuery = value;
        }
    }

    public WatcherQueryBuilder EventQueryBuilder => _eventQueryBuilder;

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
                _eventsView.Filter = CombinedEventFilter;
            }
            return _eventsView;
        }
    }

    /// <summary>
    /// Gets or sets the search text for filtering classes
    /// </summary>
    public string EventTargetClassFilter
    {
        get => _eventTargetClassFilterHelper.FilterText;
        set
        {
            if (_eventTargetClassFilterHelper.FilterText != value)
            {
                _eventTargetClassFilterHelper.FilterText = value;
                OnPropertyChanged();
            }
        }
    }

    public ReadOnlyObservableCollection<string> EventTargetClassList { get; }
    public ICollectionView EventTargetClassListView { get; }
    public ReadOnlyObservableCollection<PropertyDisplayInfo> EventTargetClassPropertyList { get; }

    /// <summary>
    /// Gets whether the query text box is read-only
    /// </summary>
    public bool IsQueryReadOnly => !IsCustomQuery;

    /// <summary>
    /// Gets a collection of watcher names from the existing watchers
    /// </summary>
    public ObservableCollection<string> WatcherNames { get; } = new();

    /// <summary>
    /// Gets the collection of watchers
    /// </summary>
    public ReadOnlyObservableCollection<WmiEventWatcherViewModel> Watchers { get; }

    /// <summary>
    /// Forces the selection logic for the currently selected event.
    /// Used by ListViewItemSelectionBehavior to re-publish the SelectedEventChangedMessage
    /// even if the same event is clicked again.
    /// </summary>
    public void ForceSelection()
    {
        if (SelectedEvent != null)
        {
            // Re-publish the message for the currently selected event
            PublishMessage(new SelectedEventChangedMessage(SelectedEvent));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _eventTargetClassFilterHelper.Dispose();
            _eventFilterDebouncer.Dispose();
            // Dispose of other managed resources if needed
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Command to add a new watcher
    /// </summary>
    [RelayCommand]
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
                EventQueryBuilder.EventQuery ?? string.Empty,
                SelectedNamespace.ManagementScope);

            // Start the watcher before adding to the collection
            watcher.Start();

            var watcherItem = new WmiEventWatcherViewModel(
                watcher,
                RemoveWatcher,
                OnEventReceived,
                EventQueryBuilder.EventClass ?? "Unknown",
                EventDisplayProperty.Name
            );
            _watchers.Add(watcherItem);

            _watcherId++; // Increment for next watcher
            PublishSuccessState($"Added watcher: {watcher.Name}");
        }
        catch (Exception ex)
        {
            PublishErrorState($"Failed to add watcher: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Command to clear events
    /// </summary>
    [RelayCommand]
    private void ClearEvents()
    {
        // Dispose each event to release unmanaged resources
        foreach (var evt in _events)
        {
            try
            {
                evt.Dispose();
            }
            catch (Exception ex)
            {
                // Log or handle disposal errors gracefully
                System.Diagnostics.Debug.WriteLine($"Error disposing WmiEvent: {ex.Message}");
            }
        }
        _events.Clear();
    }

    private bool CombinedEventFilter(object obj)
    {
        // Ensure the object is a WmiEvent
        if (obj is not WmiEvent evt)
            return false;

        // Filter by watcher name if not "All"
        if (!string.IsNullOrEmpty(SelectedWatcherName) && SelectedWatcherName != "All" && evt.WatcherName != SelectedWatcherName)
            return false;

        // Filter by event filter text (case-insensitive, checks main fields)
        if (!string.IsNullOrWhiteSpace(EventFilterText))
        {
            var filter = EventFilterText.Trim();
            bool matches =
                (!string.IsNullOrEmpty(evt.EventDisplayPropertyName) &&
                    evt.EventDisplayPropertyName.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(evt.EventDisplayPropertyValue) &&
                    evt.EventDisplayPropertyValue.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(evt.WatcherName) &&
                    evt.WatcherName.Contains(filter, StringComparison.OrdinalIgnoreCase));

            if (!matches)
                return false;
        }

        // Passed all filters
        return true;
    }

    private void EventQueryBuilder_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WatcherQueryBuilder.EventClass))
        {
            UpdateEventPropertyList();
            UpdateEventTargetClassPropertyList(); // Also update property list when EventClass changes
            UpdateEventDisplayPropertyList(); // Ensure display property list is updated when event class changes
        }
        if (e.PropertyName == nameof(WatcherQueryBuilder.EventTargetClass))
        {
            UpdateEventTargetClassPropertyList();
            UpdateEventDisplayPropertyList();
        }
        if (e.PropertyName == nameof(WatcherQueryBuilder.EventQuery))
        {
            // Update CanAddWatcher whenever the query changes
            CanAddWatcher = SelectedNamespace != null && !string.IsNullOrWhiteSpace(EventQueryBuilder.EventQuery);
        }
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

        if (SelectedNamespace != null && !string.IsNullOrEmpty(className))
        {
            // Prefer in-memory class properties if available
            var inMemoryClass = SelectedNamespace.Classes?.FirstOrDefault(c => c.ClassName == className);
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
                    var cachedProperties = await _cacheService.GetPropertiesForClassAsync(SelectedNamespace.NamespacePath, className);
                    if (cachedProperties.Count > 0)
                    {
                        propertyInfos = cachedProperties.Select(p => new PropertyDisplayInfo
                        {
                            Name = p.Name,
                            Type = p.Type ?? string.Empty
                        });
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
        if (SelectedNamespace == message.NamespaceViewModel)
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

    /// <summary>
    /// Property change handler for EventDisplayProperty
    /// </summary>
    partial void OnEventDisplayPropertyChanged(PropertyDisplayInfo value)
    {
        // Optionally, update watchers or events if needed
    }

    /// <summary>
    /// Property change handler for EventFilterText
    /// </summary>
    partial void OnEventFilterTextChanged(string value)
    {
        _eventFilterDebouncer.Debounce(() =>
        {
            RunOnUIThread(() => EventsView.Refresh());
        });
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
    /// Property change handler for SelectedEvent
    /// </summary>
    partial void OnSelectedEventChanged(WmiEvent? value)
    {
        PublishMessage(new SelectedEventChangedMessage(value));
    }

    /// <summary>
    /// Property change handler for SelectedNamespace
    /// </summary>
    partial void OnSelectedNamespaceChanged(WmiNamespaceViewModel? value)
    {
        EventQueryBuilder.EventTargetClass = string.Empty;
        if (value != null)
        {
            _ = UpdateEventTargetClassListAsync();
        }
        CanAddWatcher = SelectedNamespace != null && !string.IsNullOrWhiteSpace(EventQueryBuilder.EventQuery);
        UpdateEventClassList();
        UpdateEventPropertyList();
        UpdateEventTargetClassPropertyList(); // Also update target class properties when namespace changes
        UpdateEventDisplayPropertyList(); // Ensure display property list is updated when namespace changes
    }

    /// <summary>
    /// Property change handler for SelectedWatcherName
    /// </summary>
    partial void OnSelectedWatcherNameChanged(string? value)
    {
        EventsView.Refresh();
    }

    /// <summary>
    /// Helper to populate class lists for event or target classes
    /// </summary>
    private async Task PopulateClassListAsync(bool eventClassesOnly, ObservableCollection<string> targetCollection, ICollectionView viewToRefresh)
    {
        targetCollection.Clear();
        IEnumerable<string> classNames = Enumerable.Empty<string>();

        if (SelectedNamespace != null)
        {
            // Prefer in-memory classes if available
            var inMemory = SelectedNamespace.Classes?
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
                    var cachedClasses = await _cacheService.GetClassesForNamespaceAsync(SelectedNamespace.NamespacePath);
                    if (cachedClasses.Count > 0)
                    {
                        classNames = cachedClasses
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

    /// <summary>
    /// Command to remove all watchers
    /// </summary>
    [RelayCommand]
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

    private void RemoveWatcher(WmiEventWatcherViewModel watcher)
    {
        if (_watchers.Remove(watcher))
        {
            watcher.Dispose();
            PublishSuccessState($"Removed watcher: {watcher.Name}");
        }
    }

    /// <summary>
    /// Command to start all watchers
    /// </summary>
    [RelayCommand]
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

    /// <summary>
    /// Command to stop all watchers
    /// </summary>
    [RelayCommand]
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

        if (_eventClassList.Contains(_defaultEventclass))
        {
            EventQueryBuilder.EventClass = _defaultEventclass;
        }
        else if (_eventClassList.Count > 0 && EventQueryBuilder.EventClass != _eventClassList[0])
        {
            // Set to the first class in the list
            EventQueryBuilder.EventClass = _eventClassList[0];
        }
    }

    private void UpdateEventDisplayPropertyList()
    {
        _eventDisplayPropertyList.Clear();
        if (EventQueryBuilder.IsTargetClassEnabled && EventQueryBuilder.IsTargetClassPropertyEnabled && EventQueryBuilder.EventType != WatcherQueryBuilder.WmiEventType.Method)
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
        if (string.IsNullOrEmpty(SelectedWatcherName) || !WatcherNames.Contains(SelectedWatcherName))
        {
            if (SelectedWatcherName != "All")
                SelectedWatcherName = "All";
        }
    }
}