using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.ViewModels.Watcher;

/// <summary>
/// ViewModel for a single WMI event watcher item
/// </summary>
public partial class WmiWatcherItem : DisposableObservableObject
{
    private readonly string _eventDisplayPropertyName;
    private readonly string _eventType;
    private readonly Action<WmiEvent> _onEventReceived;
    private readonly Action<WmiWatcherItem> _onRemove;
    private readonly WmiEventWatcher _watcher;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    /// <summary>
    /// Initializes a new instance of the <see cref="WmiWatcherItem"/> class.
    /// </summary>
    public WmiWatcherItem(
        WmiEventWatcher watcher,
        Action<WmiWatcherItem> onRemove,
        Action<WmiEvent> onEventReceived,
        string eventType,
        string eventDisplayPropertyName = ""
    )
    {
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
        _onEventReceived = onEventReceived ?? throw new ArgumentNullException(nameof(onEventReceived));
        _eventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        _eventDisplayPropertyName = eventDisplayPropertyName ?? string.Empty;

        _watcher.EventArrived += OnEventArrived;

        // Initialize IsRunning from the watcher's current state
        IsRunning = _watcher.IsRunning;
    }

    /// <summary>
    /// Gets when this watcher was created
    /// </summary>
    public DateTime CreatedAt => _watcher.CreatedAt;

    public string EventDisplayPropertyName => _eventDisplayPropertyName;

    /// <summary>
    /// Gets the name of the watcher
    /// </summary>
    public string Name => _watcher.Name;

    /// <summary>
    /// Gets the namespace path this watcher is monitoring
    /// </summary>
    public string Namespace => _watcher.Namespace;

    /// <summary>
    /// Gets the WQL query used by this watcher
    /// </summary>
    public string Query => _watcher.Query;

    /// <summary>
    /// Disposes the watcher and cleans up resources
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watcher.EventArrived -= OnEventArrived;
            _watcher.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Handles the WMI event arrival
    /// </summary>
    private void OnEventArrived(object? sender, ManagementBaseObject e)
    {
        var actualEventType = e.ClassPath?.ClassName;
        if (!string.IsNullOrEmpty(_eventType) && !string.IsNullOrEmpty(actualEventType) && !string.Equals(_eventType, actualEventType, StringComparison.Ordinal))
        {
            // Ignore events that do not match the expected type
            return;
        }
        var wmiEvent = new WmiEvent(Name, e, _eventDisplayPropertyName);
        _onEventReceived(wmiEvent);
    }

    /// <summary>
    /// Command to remove the watcher
    /// </summary>
    [RelayCommand]
    private void Remove()
    {
        _onRemove(this);
    }

    /// <summary>
    /// Command to start the watcher
    /// </summary>
    [RelayCommand(CanExecute = nameof(StartCanExecute))]
    private void Start()
    {
        try
        {
            _watcher.Start();
            IsRunning = true;
        }
        catch (Exception)
        {
            // Optionally log or handle error
        }
    }

    /// <summary>
    /// Determines if the start command can be executed
    /// </summary>
    private bool StartCanExecute() => !IsRunning;

    /// <summary>
    /// Command to stop the watcher
    /// </summary>
    [RelayCommand(CanExecute = nameof(StopCanExecute))]
    private void Stop()
    {
        try
        {
            _watcher.Stop();
            IsRunning = false;
        }
        catch (Exception)
        {
            // Optionally log or handle error
        }
    }

    /// <summary>
    /// Determines if the stop command can be executed
    /// </summary>
    private bool StopCanExecute() => IsRunning;
}