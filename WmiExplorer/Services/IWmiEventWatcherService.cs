using System;
using System.Management;

namespace WmiExplorer.Services
{
    /// <summary>
    /// Service for managing WMI event watchers
    /// </summary>
    public interface IWmiEventWatcherService : IDisposable
    {
        /// <summary>
        /// Event raised when a WMI event is received
        /// </summary>
        event EventHandler<ManagementBaseObject>? EventArrived;

        /// <summary>
        /// Starts watching for WMI events using the specified scope and query
        /// </summary>
        /// <param name="scope">The WMI scope to watch</param>
        /// <param name="query">The WQL query for events</param>
        void StartWatching(ManagementScope scope, string query);

        /// <summary>
        /// Stops watching for WMI events
        /// </summary>
        void StopWatching();
    }
} 