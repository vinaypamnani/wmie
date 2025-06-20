using System.ComponentModel;
using System.Management;

namespace WmiExplorer.Core.Models;

/// <summary>
/// Represents a WMI event watcher that can be started and stopped.
/// </summary>
public class WmiEventWatcher : IDisposable
{
    /// <summary>
    /// Event raised when a WMI event is received
    /// </summary>
    public event EventHandler<ManagementBaseObject>? EventArrived;

    private bool _isRunning;
    private ManagementEventWatcher? _managementEventWatcher;
    private readonly ManagementScope _scope;

    /// <summary>
    /// Initializes a new instance of the <see cref="WmiEventWatcher"/> class.
    /// </summary>
    /// <param name="name">The name of the watcher</param>
    /// <param name="query">The WQL query for events</param>
    /// <param name="scope">The WMI scope to watch</param>
    public WmiEventWatcher(string name, string query, ManagementScope scope, string eventClass, string displayPropertyName)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));

        CreatedAt = DateTime.Now;
        Name = name;
        Query = query ?? throw new ArgumentNullException(nameof(query));
        EventClass = eventClass ?? throw new ArgumentNullException(nameof(eventClass));
        DisplayPropertyName = displayPropertyName ?? string.Empty;
        Namespace = scope.Path.NamespacePath;
    }

    /// <summary>
    /// Gets when this watcher was created
    /// </summary>
    [Category("Event Watcher")]
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the display property name for events
    /// </summary>
    [Category("Event Watcher")]
    public string DisplayPropertyName { get; }

    /// <summary>
    /// Gets the event class name this watcher is monitoring
    /// </summary>
    [Category("Event Watcher")]
    public string EventClass { get; }

    /// <summary>
    /// Gets whether the watcher is currently running
    /// </summary>
    [Category("Event Watcher")]
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the name of the watcher
    /// </summary>
    [Category("Event Watcher")]
    public string Name { get; }

    /// <summary>
    /// Gets the namespace path this watcher is monitoring
    /// </summary>
    [Category("Event Watcher")]
    public string Namespace { get; }

    /// <summary>
    /// Gets the WQL query used by this watcher
    /// </summary>
    [Category("Event Watcher")]
    public string Query { get; }

    /// <summary>
    /// Starts watching for WMI events
    /// </summary>
    public void Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WmiEventWatcher));

        if (_isRunning)
            return;

        try
        {
            _managementEventWatcher = new ManagementEventWatcher(_scope, new EventQuery(Query));
            _managementEventWatcher.EventArrived += OnEventArrived;
            _managementEventWatcher.Start();
            _isRunning = true;
        }
        catch (Exception)
        {
            _managementEventWatcher?.Dispose();
            _managementEventWatcher = null;
            _isRunning = false;
            throw;
        }
    }

    /// <summary>
    /// Stops watching for WMI events
    /// </summary>
    public void Stop()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WmiEventWatcher));

        if (!_isRunning)
            return;

        try
        {
            if (_managementEventWatcher != null)
            {
                _managementEventWatcher.EventArrived -= OnEventArrived;
                _managementEventWatcher.Stop();
                _managementEventWatcher.Dispose();
                _managementEventWatcher = null;
            }
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// Handles the event when a WMI event is received
    /// </summary>
    private void OnEventArrived(object? sender, EventArrivedEventArgs e)
    {
        EventArrived?.Invoke(this, e.NewEvent);
    }

    #region IDisposable
    private bool _disposed;

    /// <summary>
    /// Disposes the watcher and stops watching
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }

    #endregion
}