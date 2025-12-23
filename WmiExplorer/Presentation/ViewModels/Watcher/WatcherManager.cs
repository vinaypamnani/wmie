using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Watcher;

/// <summary>
/// Manages the collection of WMI event watchers with lifecycle management
/// </summary>
public class WatcherManager : DisposableObservableObject
{
    /// <summary>
    /// Event raised when the watcher collection changes
    /// </summary>
    public event EventHandler? WatchersChanged;

    /// <summary>
    /// Event raised when an individual watcher's state changes
    /// </summary>
    public event EventHandler<WmiEventWatcherViewModel>? WatcherStateChanged;

    private readonly IMessengerService _messengerService;
    private int _watcherId = 1;
    private readonly ObservableCollection<WmiEventWatcherViewModel> _watchers = new();

    /// <summary>
    /// Initializes a new instance of the WatcherManager class
    /// </summary>
    /// <param name="messengerService">The messenger service for status updates</param>
    public WatcherManager(IMessengerService messengerService)
    {
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));

        Watchers = new ReadOnlyObservableCollection<WmiEventWatcherViewModel>(_watchers);

        // Monitor watchers collection for changes
        _watchers.CollectionChanged += OnWatchersCollectionChanged;
        UpdateWatcherNames();
    }

    /// <summary>
    /// Gets the collection of watcher names for UI binding
    /// </summary>
    public ObservableCollection<string> WatcherNames { get; } = new();

    /// <summary>
    /// Gets the read-only collection of watchers
    /// </summary>
    public ReadOnlyObservableCollection<WmiEventWatcherViewModel> Watchers { get; }

    /// <summary>
    /// Adds a new watcher with the specified parameters
    /// </summary>
    /// <param name="query">The WQL query for the watcher</param>
    /// <param name="scope">The management scope</param>
    /// <param name="eventClass">The event class name</param>
    /// <param name="targetClass">The target class name (optional)</param>
    /// <param name="displayProperty">The display property name</param>
    /// <param name="onEventReceived">Callback for received events</param>
    /// <returns>True if the watcher was added successfully</returns>
    public bool AddWatcher(string query, System.Management.ManagementScope scope, string eventClass, string? targetClass, string displayProperty, Action<WmiEvent> onEventReceived)
    {
        if (string.IsNullOrWhiteSpace(query) || scope == null)
            return false;

        try
        {
            // Compose watcher name as <EventType>_<TargetClass>_<Id>
            string watcherName = string.IsNullOrWhiteSpace(eventClass) ? "Unknown" : eventClass;
            if (!string.IsNullOrWhiteSpace(targetClass))
                watcherName += "_" + targetClass;
            watcherName += $"_{_watcherId}";

            var watcher = new WmiEventWatcher(watcherName, query, scope, eventClass, displayProperty);

            // Start the watcher before adding to the collection
            watcher.Start();
            var watcherItem = new WmiEventWatcherViewModel(
                watcher,
                w => RemoveWatcher(w),
                onEventReceived
            );

            // Subscribe to watcher property changes
            watcherItem.PropertyChanged += OnWatcherPropertyChanged;

            _watchers.Add(watcherItem);
            _watcherId++; // Increment for next watcher

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add watcher for event class {EventClass} with query {Query}", eventClass, query);
            return false;
        }
    }

    /// <summary>
    /// Clears all watchers from the collection
    /// </summary>
    public void Clear()
    {
        RemoveAllWatchers();
    }

    /// <summary>
    /// Gets the count of currently running watchers
    /// </summary>
    /// <returns>The number of running watchers</returns>
    public int GetRunningWatcherCount()
    {
        return _watchers.Count(w => w.IsRunning);
    }

    /// <summary>
    /// Removes all watchers from the collection
    /// </summary>
    /// <returns>The number of watchers that were removed</returns>
    public int RemoveAllWatchers()
    {
        var toRemove = _watchers.ToList();
        int count = 0;

        foreach (var watcher in toRemove)
        {
            if (RemoveWatcher(watcher))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Removes the specified watcher
    /// </summary>
    /// <param name="watcher">The watcher to remove</param>
    /// <returns>True if the watcher was removed successfully</returns>
    public bool RemoveWatcher(WmiEventWatcherViewModel watcher)
    {
        if (watcher == null)
            return false;

        if (_watchers.Remove(watcher))
        {
            // Unsubscribe from property changes before disposing
            watcher.PropertyChanged -= OnWatcherPropertyChanged;
            watcher.Dispose();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes watchers for a specific namespace and all its children
    /// </summary>
    /// <param name="namespacePath">The namespace path to remove watchers for</param>
    /// <returns>The number of watchers that were removed</returns>
    public int RemoveWatchersForNamespace(string namespacePath)
    {
        if (string.IsNullOrEmpty(namespacePath))
            return 0;

        var toRemove = _watchers.Where(w =>
            w.Namespace.StartsWith(namespacePath, StringComparison.OrdinalIgnoreCase)).ToList();

        int count = 0;
        foreach (var watcher in toRemove)
        {
            if (RemoveWatcher(watcher))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Starts all watchers that are not currently running
    /// </summary>
    /// <returns>The number of watchers that were started</returns>
    public int StartAllWatchers()
    {
        int count = 0;
        foreach (var watcher in _watchers)
        {
            if (watcher.StartCommand.CanExecute(null))
            {
                watcher.StartCommand.Execute(null);
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Stops all watchers that are currently running
    /// </summary>
    /// <returns>The number of watchers that were stopped</returns>
    public int StopAllWatchers()
    {
        int count = 0;
        foreach (var watcher in _watchers)
        {
            if (watcher.StopCommand.CanExecute(null))
            {
                watcher.StopCommand.Execute(null);
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Disposes the manager and all watchers
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watchers.CollectionChanged -= OnWatchersCollectionChanged;
            Clear();
            WatcherNames.Clear();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Handles property changes from individual watchers
    /// </summary>
    private void OnWatcherPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is WmiEventWatcherViewModel watcher && e.PropertyName == nameof(WmiEventWatcherViewModel.IsRunning))
        {
            WatcherStateChanged?.Invoke(this, watcher);
        }
    }

    /// <summary>
    /// Handles changes to the watchers collection
    /// </summary>
    private void OnWatchersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateWatcherNames();
        WatchersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the watcher names collection for UI binding
    /// </summary>
    private void UpdateWatcherNames()
    {
        var names = _watchers.Select(w => w.Name).Distinct().OrderBy(n => n).ToList();

        // Build the new collection with "All" first, then watcher names
        var newNames = new List<string> { "All" };
        newNames.AddRange(names);

        // Update collection efficiently to preserve ComboBox selection
        // Remove items not in new list
        for (int i = WatcherNames.Count - 1; i >= 0; i--)
        {
            if (!newNames.Contains(WatcherNames[i]))
                WatcherNames.RemoveAt(i);
        }

        // Add items that are in new list but not in current collection
        foreach (var name in newNames)
        {
            if (!WatcherNames.Contains(name))
                WatcherNames.Add(name);
        }

        // Ensure "All" is first if it's not already
        if (WatcherNames.Count > 0 && WatcherNames[0] != "All")
        {
            var allIndex = WatcherNames.IndexOf("All");
            if (allIndex > 0)
            {
                WatcherNames.Move(allIndex, 0);
            }
        }
    }
}