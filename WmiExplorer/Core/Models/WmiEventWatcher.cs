using System;
using System.Management;
using WmiExplorer.Services;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Represents a WMI event watcher that can be started and stopped.
    /// </summary>
    public class WmiEventWatcher : IDisposable
    {
        private readonly IWmiEventWatcherService _eventWatcherService;
        private readonly string _query;
        private readonly ManagementScope _scope;
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
        /// <param name="query">The WQL query for events</param>
        /// <param name="scope">The WMI scope to watch</param>
        /// <param name="eventWatcherService">The WMI event watcher service to use</param>
        public WmiEventWatcher(string query, ManagementScope scope, IWmiEventWatcherService eventWatcherService)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _eventWatcherService = eventWatcherService ?? throw new ArgumentNullException(nameof(eventWatcherService));
            
            Name = $"Watcher_{DateTime.Now:yyyyMMdd_HHmmss}";
            CreatedAt = DateTime.Now;

            // Subscribe to the service's event
            _eventWatcherService.EventArrived += OnEventArrived;
        }

        /// <summary>
        /// Starts watching for WMI events
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WmiEventWatcher));

            if (!_isRunning)
            {
                _eventWatcherService.StartWatching(_scope, _query);
                _isRunning = true;
            }
        }

        /// <summary>
        /// Stops watching for WMI events
        /// </summary>
        public void Stop()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WmiEventWatcher));

            if (_isRunning)
            {
                _eventWatcherService.StopWatching();
                _isRunning = false;
            }
        }

        /// <summary>
        /// Handles the event when a WMI event is received
        /// </summary>
        private void OnEventArrived(object? sender, ManagementBaseObject e)
        {
            EventArrived?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the watcher and stops watching
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _eventWatcherService.EventArrived -= OnEventArrived;
                _disposed = true;
            }
        }
    }
} 