using System;
using System.Management;

namespace WmiExplorer.Services
{
    /// <summary>
    /// Service for managing WMI event watchers
    /// </summary>
    public class WmiEventWatcherService : IWmiEventWatcherService
    {
        private ManagementEventWatcher? _watcher;
        private bool _disposed;

        /// <summary>
        /// Event raised when a WMI event is received
        /// </summary>
        public event EventHandler<ManagementBaseObject>? EventArrived;

        /// <summary>
        /// Starts watching for WMI events using the specified scope and query
        /// </summary>
        /// <param name="scope">The WMI scope to watch</param>
        /// <param name="query">The WQL query for events</param>
        public void StartWatching(ManagementScope scope, string query)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WmiEventWatcherService));

            StopWatching();

            try
            {
                _watcher = new ManagementEventWatcher(scope, new EventQuery(query));
                _watcher.EventArrived += OnEventArrived;
                _watcher.Start();
            }
            catch (Exception ex)
            {
                StopWatching();
                throw new InvalidOperationException($"Failed to start WMI event watcher: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Stops watching for WMI events
        /// </summary>
        public void StopWatching()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WmiEventWatcherService));

            if (_watcher != null)
            {
                _watcher.EventArrived -= OnEventArrived;
                _watcher.Stop();
                _watcher.Dispose();
                _watcher = null;
            }
        }

        /// <summary>
        /// Handles the event when a WMI event is received
        /// </summary>
        private void OnEventArrived(object sender, EventArrivedEventArgs e)
        {
            EventArrived?.Invoke(this, e.NewEvent);
        }

        /// <summary>
        /// Disposes the service and stops watching
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                StopWatching();
                _disposed = true;
            }
        }
    }
} 