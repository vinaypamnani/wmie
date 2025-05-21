using System;
using System.Management;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Represents a WMI event watcher that can be started and stopped.
    /// </summary>
    public class WmiEventWatcher : IDisposable
    {
        private readonly string _query;
        private readonly ManagementScope _scope;
        private ManagementEventWatcher? _managementEventWatcher;
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        /// Gets the name of the watcher
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the WQL query used by this watcher
        /// </summary>
        public string Query => _query;

        /// <summary>
        /// Gets the namespace path this watcher is monitoring
        /// </summary>
        public string Namespace => _scope.Path.NamespacePath;

        /// <summary>
        /// Gets whether the watcher is currently running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets when this watcher was created
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Event raised when a WMI event is received
        /// </summary>
        public event EventHandler<ManagementBaseObject>? EventArrived;

        /// <summary>
        /// Initializes a new instance of the <see cref="WmiEventWatcher"/> class.
        /// </summary>
        /// <param name="name">The name of the watcher</param>
        /// <param name="query">The WQL query for events</param>
        /// <param name="scope">The WMI scope to watch</param>
        public WmiEventWatcher(string name, string query, ManagementScope scope)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            Name = name;
            CreatedAt = DateTime.Now;
        }

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
                _managementEventWatcher = new ManagementEventWatcher(_scope, new EventQuery(_query));
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
    }
}