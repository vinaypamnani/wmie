using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.ViewModels.Watcher;

/// <summary>
/// Manages the collection of WMI events with filtering and lifecycle management
/// </summary>
public partial class EventManager : DisposableObservableObject
{
    private readonly DebounceDispatcher _eventFilterDebouncer = new();

    [ObservableProperty]
    private string _eventFilterText = string.Empty;

    private readonly ObservableCollection<WmiEvent> _events = new();
    private ICollectionView? _eventsView;

    [ObservableProperty]
    private string? _selectedWatcherName;

    /// <summary>
    /// Initializes a new instance of the EventManager class
    /// </summary>
    public EventManager()
    {
        Events = new ReadOnlyObservableCollection<WmiEvent>(_events);
        TrackDisposable(_eventFilterDebouncer);
    }

    /// <summary>
    /// Gets the read-only collection of events
    /// </summary>
    public ReadOnlyObservableCollection<WmiEvent> Events { get; }

    /// <summary>
    /// Gets the filtered collection view for events
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
    /// Maximum number of events to keep in memory
    /// </summary>
    public int MaxEvents { get; set; } = 1000;

    /// <summary>
    /// Adds a new event to the collection
    /// </summary>
    /// <param name="wmiEvent">The event to add</param>
    public void AddEvent(WmiEvent wmiEvent)
    {
        if (wmiEvent == null)
            return;

        _events.Add(wmiEvent);

        // Remove old events if we exceed the maximum
        while (_events.Count > MaxEvents)
        {
            var oldEvent = _events[0];
            _events.RemoveAt(0);
            try
            {
                oldEvent.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing WmiEvent during cleanup");
            }
        }

        // Notify that properties dependent on collection count have changed
        OnPropertyChanged(nameof(Events));
    }

    /// <summary>
    /// Clears all events from the collection
    /// </summary>
    public void ClearEvents()
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
                Log.Warning(ex, "Error disposing WmiEvent during clear operation");
            }
        }
        _events.Clear();

        // Notify that properties dependent on collection count have changed
        OnPropertyChanged(nameof(Events));
    }

    /// <summary>
    /// Gets the total count of events in the collection
    /// </summary>
    /// <returns>The number of events</returns>
    public int GetEventCount()
    {
        return _events.Count;
    }

    /// <summary>
    /// Gets the count of events for a specific watcher
    /// </summary>
    /// <param name="watcherName">The watcher name to filter by</param>
    /// <returns>The number of events for the specified watcher</returns>
    public int GetEventCountForWatcher(string watcherName)
    {
        if (string.IsNullOrEmpty(watcherName) || watcherName == "All")
            return _events.Count;

        return _events.Count(e => e.WatcherName == watcherName);
    }

    /// <summary>
    /// Disposes the manager and clears all events
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearEvents();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Combined filter for events based on watcher name and filter text
    /// </summary>
    /// <param name="obj">The object to filter</param>
    /// <returns>True if the object should be included in the view</returns>
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

    /// <summary>
    /// Partial method called when EventFilterText changes
    /// </summary>
    partial void OnEventFilterTextChanged(string value)
    {
        RefreshFilterWithDebounce();
    }

    /// <summary>
    /// Partial method called when SelectedWatcherName changes
    /// </summary>
    partial void OnSelectedWatcherNameChanged(string? value)
    {
        EventsView.Refresh();
    }

    /// <summary>
    /// Refreshes the events view filter with debouncing
    /// </summary>
    private void RefreshFilterWithDebounce()
    {
        _eventFilterDebouncer.Debounce(() =>
        {
            EventsView.Refresh();
        });
    }
}