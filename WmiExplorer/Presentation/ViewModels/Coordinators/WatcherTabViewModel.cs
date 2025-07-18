using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Presentation.ViewModels.Watcher;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// View model for WMI Event Watcher tab - refactored to use manager classes for better separation of concerns
/// </summary>
public partial class WatcherTabViewModel : SelectionAwareViewModelBase
{
    private readonly ClassListManager _classListManager;

    [ObservableProperty]
    private PropertyDisplayInfo? _displayProperty;

    private readonly DisplayPropertyManager _displayPropertyManager;
    private readonly EventManager _eventManager;
    private readonly PropertyListManager _eventPropertyManager;
    private readonly WatcherQueryBuilder _eventQueryBuilder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQueryReadOnly))]
    private bool _isCustomQuery = false;

    [ObservableProperty]
    private int _maxEvents = 1000;

    // Backing property for the MaxEvents input field
    [ObservableProperty]
    private string _maxEventsInput = "1000";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEvent))]
    private WmiEvent? _selectedEvent;

    [ObservableProperty]
    private TabStatus _tabStatus;

    private readonly PropertyListManager _targetPropertyManager;
    private readonly WatcherManager _watcherManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatcherTabViewModel"/> class.
    /// </summary>
    /// <param name="messengerService">The messenger service to use</param>
    /// <param name="cacheService">The cache service to use</param>
    /// <param name="selectionManager">The selection service to use</param>
    public WatcherTabViewModel(
        IMessengerService messengerService,
        ICacheService cacheService,
        SelectionManager selectionManager) : base(messengerService, selectionManager)
    {
        // Initialize managers
        _eventPropertyManager = TrackDisposable(new PropertyListManager(cacheService));
        _targetPropertyManager = TrackDisposable(new PropertyListManager(cacheService));
        _displayPropertyManager = TrackDisposable(new DisplayPropertyManager());
        _classListManager = TrackDisposable(new ClassListManager(cacheService));
        _watcherManager = TrackDisposable(new WatcherManager(messengerService));
        _eventManager = TrackDisposable(new EventManager(MaxEvents));

        _eventQueryBuilder = new WatcherQueryBuilder();

        // Initialize tab status with messenger service
        _tabStatus = new TabStatus(messengerService, AppState.Ready, "Build an event monitor query and click Add Watcher to start monitoring.", "Monitor WMI events");

        // Subscribe to essential messages only
        StrongSubscribe<ClassesLoadedMessage>(HandleClassesLoadedMessage);

        // Wire up cross-manager dependencies
        SetupManagerInteractions();

        // Subscribe to builder property changes for UI sync and command state updates
        _eventQueryBuilder.PropertyChanged += EventQueryBuilder_PropertyChanged;

        // Subscribe to events collection changes to update HasEvents property
        _eventManager.PropertyChanged += EventManager_PropertyChanged;

        // Sync MaxEvents changes to EventManager
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MaxEvents))
            {
                _eventManager.SetMaxEvents(MaxEvents);
            }
        };
    }

    public ReadOnlyObservableCollection<string> EventClassList => _classListManager.EventClasses;
    public ICollectionView EventClassListView => _classListManager.EventClassListView;
    public ReadOnlyObservableCollection<PropertyDisplayInfo> EventDisplayPropertyList => _displayPropertyManager.Properties;

    /// <summary>
    /// Gets or sets the event filter text
    /// </summary>
    public string EventFilterText
    {
        get => _eventManager.EventFilterText;
        set => SetProperty(_eventManager.EventFilterText, value, _eventManager, (manager, newValue) => manager.EventFilterText = newValue);
    }

    // Expose manager properties for UI binding
    public ReadOnlyObservableCollection<PropertyDisplayInfo> EventPropertyList => _eventPropertyManager.Properties;

    /// <summary>
    /// Gets or sets the WMI event query
    /// </summary>
    public string EventQuery
    {
        get => EventQueryBuilder.EventQuery ?? string.Empty;
        set => SetProperty(EventQueryBuilder.EventQuery ?? string.Empty, value, EventQueryBuilder, (builder, newValue) => builder.EventQuery = newValue);
    }

    public WatcherQueryBuilder EventQueryBuilder => _eventQueryBuilder;
    public ReadOnlyObservableCollection<WmiEvent> Events => _eventManager.Events;
    public ICollectionView EventsView => _eventManager.EventsView;

    /// <summary>
    /// Gets or sets the search text for filtering target classes
    /// </summary>
    public string EventTargetClassFilter
    {
        get => _classListManager.TargetClassFilter;
        set => SetProperty(_classListManager.TargetClassFilter, value, _classListManager, (manager, newValue) => manager.TargetClassFilter = newValue);
    }

    public ReadOnlyObservableCollection<string> EventTargetClassList => _classListManager.TargetClasses;
    public ICollectionView EventTargetClassListView => _classListManager.TargetClassListView;
    public ReadOnlyObservableCollection<PropertyDisplayInfo> EventTargetClassPropertyList => _targetPropertyManager.Properties;

    /// <summary>
    /// Gets whether there is an active namespace selected
    /// </summary>
    public bool HasActiveNamespace => SelectionManager.SelectedNamespace != null;

    /// <summary>
    /// Gets whether there are any events in the collection
    /// </summary>
    public bool HasEvents => Events.Count > 0;

    /// <summary>
    /// Gets whether there is a selected event
    /// </summary>
    public bool HasSelectedEvent => SelectedEvent != null;

    /// <summary>
    /// Gets whether there are any watchers active
    /// </summary>
    public bool HasWatchers => Watchers.Count > 0;

    /// <summary>
    /// Gets whether the query text box is read-only
    /// </summary>
    public bool IsQueryReadOnly => !IsCustomQuery;

    /// <summary>
    /// Gets or sets the selected watcher name for filtering events
    /// </summary>
    public string? SelectedWatcherName
    {
        get => _eventManager.SelectedWatcherName;
        set => SetProperty(_eventManager.SelectedWatcherName, value, _eventManager, (manager, newValue) => manager.SelectedWatcherName = newValue);
    }

    /// <summary>
    /// Gets the header text for the Watcher tab with count
    /// </summary>
    public string TabHeader
    {
        get
        {
            var filteredCount = EventsView?.Cast<object>().Count();
            if (filteredCount.HasValue && filteredCount.Value > 0)
            {
                return $"Watcher [{filteredCount.Value}]";
            }
            return "Watcher";
        }
    }

    public ObservableCollection<string> WatcherNames => _watcherManager.WatcherNames;
    public ReadOnlyObservableCollection<WmiEventWatcherViewModel> Watchers => _watcherManager.Watchers;

    /// <summary>
    /// Clears events for watchers of a specific namespace and all its children
    /// </summary>
    /// <param name="namespacePath">The namespace path to clear events for</param>
    /// <returns>The number of events that were cleared</returns>
    public int ClearEventsForNamespace(string namespacePath)
    {
        if (string.IsNullOrEmpty(namespacePath))
            return 0;

        // Get watcher names for the specified namespace (now using full paths)
        var watcherNames = Watchers
            .Where(w => w.Namespace.StartsWith(namespacePath, StringComparison.OrdinalIgnoreCase))
            .Select(w => w.Name)
            .ToList();

        // Clear events for those watchers
        return _eventManager.ClearEventsForWatchers(watcherNames);
    }

    /// <summary>
    /// Removes watchers for a specific namespace and all its children
    /// </summary>
    /// <param name="namespacePath">The namespace path to remove watchers for</param>
    /// <returns>The number of watchers that were removed</returns>
    public int RemoveWatchersForNamespace(string namespacePath)
    {
        return _watcherManager.RemoveWatchersForNamespace(namespacePath);
    }

    /// <summary>
    /// Called when the selected namespace changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedNamespaceChanged(WmiNamespaceViewModel? selectedNamespace)
    {
        // Notify UI updates - no local property to notify
        OnPropertyChanged(nameof(HasActiveNamespace));
        AddWatcherCommand.NotifyCanExecuteChanged();
        SetMaxEventsCommand.NotifyCanExecuteChanged();

        // Handle namespace change logic
        EventQueryBuilder.EventTargetClass = string.Empty;
        if (selectedNamespace != null)
        {
            _ = UpdateClassListsAndPropertiesAsync();
        }
    }

    /// <summary>
    /// Command to add a new watcher
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddWatcher))]
    private void AddWatcher()
    {
        if (SelectionManager.SelectedNamespace == null)
        {
            TabStatus.SetError("No namespace selected.");
            return;
        }

        try
        {
            var success = _watcherManager.AddWatcher(
                EventQueryBuilder.EventQuery ?? string.Empty,
                SelectionManager.SelectedNamespace.ManagementScope,
                EventQueryBuilder.EventClass ?? "Unknown",
                EventQueryBuilder.EventTargetClass,
                DisplayProperty?.Name ?? string.Empty,
                OnEventReceived
            );

            if (success)
            {
                Log.Information("Watcher added successfully: EventClass={EventClass}, EventQuery={EventQuery}",
                    EventQueryBuilder.EventClass ?? "Unknown", EventQueryBuilder.EventQuery ?? "Empty");
                TabStatus.SetSuccess("Watcher added successfully.");
            }
            else
            {
                Log.Warning("Failed to add watcher: EventClass={EventClass}, EventQuery={EventQuery}",
                    EventQueryBuilder.EventClass ?? "Unknown", EventQueryBuilder.EventQuery ?? "Empty");
                TabStatus.SetError("Failed to add watcher.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred while adding watcher: EventClass={EventClass}, EventQuery={EventQuery}",
                EventQueryBuilder.EventClass ?? "Unknown", EventQueryBuilder.EventQuery ?? "Empty");
            TabStatus.SetError($"Error adding watcher: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Determines if the AddWatcher command can execute
    /// </summary>
    private bool CanAddWatcher() => HasActiveNamespace && !string.IsNullOrWhiteSpace(EventQueryBuilder?.EventQuery);

    /// <summary>
    /// Determines if the ClearEvents command can execute
    /// </summary>
    private bool CanClearEvents() => HasEvents;

    /// <summary>
    /// Determines if the RemoveAllWatchers command can execute
    /// </summary>
    private bool CanRemoveAllWatchers() => HasWatchers;

    /// <summary>
    /// Determines if the SetMaxEvents command can execute
    /// </summary>
    private bool CanSetMaxEvents() => HasActiveNamespace;

    /// <summary>
    /// Determines if the StartAllWatchers command can execute
    /// </summary>
    private bool CanStartAllWatchers() => HasWatchers && Watchers.Any(w => !w.IsRunning);

    /// <summary>
    /// Determines if the StopAllWatchers command can execute
    /// </summary>
    private bool CanStopAllWatchers() => HasWatchers && Watchers.Any(w => w.IsRunning);

    /// <summary>
    /// Command to clear events
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearEvents))]
    private void ClearEvents()
    {
        _eventManager.ClearEvents();
        TabStatus.SetSuccess("Events cleared.");
    }

    /// <summary>
    /// Handles property changes from the event manager
    /// </summary>
    private void EventManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_eventManager.Events))
        {
            // Update tab header
            OnPropertyChanged(nameof(TabHeader));

            // Update HasEvents property for UI binding
            OnPropertyChanged(nameof(HasEvents));
            ClearEventsCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Handles property changes from the query builder
    /// </summary>
    private void EventQueryBuilder_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WatcherQueryBuilder.EventClass))
        {
            _ = UpdateEventPropertiesAsync();
            UpdateDisplayPropertyList();
        }
        if (e.PropertyName == nameof(WatcherQueryBuilder.EventTargetClass))
        {
            _ = UpdateTargetClassPropertiesAsync();
            UpdateDisplayPropertyList();
        }
        if (e.PropertyName == nameof(WatcherQueryBuilder.EventQuery))
        {
            // Notify that command can execute state changed
            AddWatcherCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Handles when classes are loaded in a namespace
    /// </summary>
    private void HandleClassesLoadedMessage(ClassesLoadedMessage message)
    {
        if (message?.NamespaceViewModel == null)
            return;

        // Only update if this is our selected namespace
        if (SelectionManager.SelectedNamespace == message.NamespaceViewModel)
        {
            _ = UpdateClassListsAndPropertiesAsync();
        }
    }

    /// <summary>
    /// Called when a WMI event is received from a watcher
    /// </summary>
    private void OnEventReceived(WmiEvent wmiEvent)
    {
        RunOnUIThread(() =>
        {
            _eventManager.AddEvent(wmiEvent);
        });
    }

    /// <summary>
    /// Add a partial method to handle MaxEvents changes
    /// </summary>
    partial void OnMaxEventsChanged(int value)
    {
        _eventManager.SetMaxEvents(value);
        MaxEventsInput = value.ToString(); // keep input in sync
    }

    partial void OnSelectedEventChanged(WmiEvent? value)
    {
        if (value != null)
        {
            TabStatus.SetSuccess($"Selected event: {value.EventClassName}.{value.EventDisplayPropertyName}={value.EventDisplayPropertyValue} at {value.EventTimestamp}");
        }
    }

    /// <summary>
    /// Command to remove all watchers
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveAllWatchers))]
    private void RemoveAllWatchers()
    {
        var count = _watcherManager.RemoveAllWatchers();
        if (SelectionManager.PropertyGrid.SelectedObjectForPropertyGrid is WmiEventWatcher)
        {
            // If the property grid is showing a watcher, clear it
            SelectionManager.PropertyGrid.ClearPropertyGrid();
        }

        PublishSuccessState($"Removed {count} watchers.");
    }

    /// <summary>
    /// RelayCommand to set MaxEvents from the input field
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSetMaxEvents))]
    private void SetMaxEvents()
    {
        if (int.TryParse(MaxEventsInput, out int value) && value > 0)
        {
            MaxEvents = value;
        }
        else
        {
            Log.Warning("Invalid MaxEvents input: {InputValue}. Must be a positive integer. Resetting to {MaxEvents}", MaxEventsInput, MaxEvents);
            MaxEventsInput = MaxEvents.ToString();
        }
    }

    /// <summary>
    /// Sets up interactions between managers
    /// </summary>
    private void SetupManagerInteractions()
    {
        // Set initial default watcher name to "All"
        SelectedWatcherName = "All";

        // When watchers change, reset selection if it becomes invalid or null
        _watcherManager.WatchersChanged += (s, e) =>
        {
            // Reset to "All" if selection is null (removed watcher) or no longer valid
            // Since UpdateWatcherNames() no longer clears the collection, this won't
            // interfere with user selections when adding watchers
            if (SelectedWatcherName == null || !WatcherNames.Contains(SelectedWatcherName))
            {
                SelectedWatcherName = "All";
            }

            // Notify computed properties and commands that state changed
            OnPropertyChanged(nameof(HasWatchers));
            RemoveAllWatchersCommand.NotifyCanExecuteChanged();
            StartAllWatchersCommand.NotifyCanExecuteChanged();
            StopAllWatchersCommand.NotifyCanExecuteChanged();
        };

        // Subscribe to individual watcher state changes
        _watcherManager.WatcherStateChanged += (s, watcher) =>
        {
            // Update command states when individual watcher states change
            StartAllWatchersCommand.NotifyCanExecuteChanged();
            StopAllWatchersCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>
    /// Command to start all watchers
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartAllWatchers))]
    private void StartAllWatchers()
    {
        var count = _watcherManager.StartAllWatchers();
        SelectionManager.PropertyGrid.RefreshPropertyGrid();
        PublishSuccessState($"Started {count} watchers.");
    }

    /// <summary>
    /// Command to stop all watchers
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopAllWatchers))]
    private void StopAllWatchers()
    {
        var count = _watcherManager.StopAllWatchers();
        SelectionManager.PropertyGrid.RefreshPropertyGrid();
        PublishSuccessState($"Stopped {count} watchers.");
    }

    /// <summary>
    /// Updates all class lists and related properties
    /// </summary>
    private async Task UpdateClassListsAndPropertiesAsync()
    {
        try
        {
            await _classListManager.UpdateClassListsAsync(SelectionManager.SelectedNamespace);

            // Set default event class if available
            var defaultClass = _classListManager.GetDefaultOrFirstEventClass();
            if (defaultClass != null && EventQueryBuilder.EventClass != defaultClass)
            {
                EventQueryBuilder.EventClass = defaultClass;
            }

            await UpdateEventPropertiesAsync();
            await UpdateTargetClassPropertiesAsync();
            UpdateDisplayPropertyList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update class lists and properties for namespace: {NamespacePath}",
                SelectionManager.SelectedNamespace?.NamespacePath ?? "Unknown");
        }
    }

    /// <summary>
    /// Updates the display property list based on current query builder state
    /// </summary>
    private void UpdateDisplayPropertyList()
    {
        _displayPropertyManager.UpdateDisplayPropertyList(
            EventQueryBuilder,
            _eventPropertyManager,
            _targetPropertyManager
        );

        // Set default display property if needed
        DisplayProperty = _displayPropertyManager.GetDefaultDisplayProperty();
    }

    /// <summary>
    /// Updates event properties for the selected event class
    /// </summary>
    private async Task UpdateEventPropertiesAsync()
    {
        try
        {
            await _eventPropertyManager.UpdatePropertiesAsync(SelectionManager.SelectedNamespace, EventQueryBuilder.EventClass);

            if (_eventPropertyManager.Properties.Count > 0)
            {
                // Set preferred event property
                var preferredProperty = _eventPropertyManager.GetPreferredProperty(new Func<PropertyDisplayInfo, bool>[]
                {
                    p => p.Name.StartsWith("Target", StringComparison.OrdinalIgnoreCase)
                });

                if (preferredProperty != null)
                {
                    EventQueryBuilder.EventProperty = preferredProperty;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update event properties for class: {EventClass}",
                EventQueryBuilder.EventClass ?? "Unknown");
        }
    }

    /// <summary>
    /// Updates target class properties for the selected target class
    /// </summary>
    private async Task UpdateTargetClassPropertiesAsync()
    {
        try
        {
            await _targetPropertyManager.UpdatePropertiesAsync(SelectionManager.SelectedNamespace, EventQueryBuilder.EventTargetClass);

            if (_targetPropertyManager.Properties.Count > 0)
            {
                // Set preferred target class property
                var preferredProperty = _targetPropertyManager.GetPreferredProperty(new Func<PropertyDisplayInfo, bool>[]
                {
                    p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase),
                    p => p.Name.EndsWith("Name", StringComparison.OrdinalIgnoreCase),
                    p => p.Name.Contains("Display", StringComparison.OrdinalIgnoreCase)
                });

                if (preferredProperty != null)
                {
                    EventQueryBuilder.EventTargetClassProperty = preferredProperty;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update target class properties for class: {EventTargetClass}",
                EventQueryBuilder.EventTargetClass ?? "Unknown");
        }
    }
}