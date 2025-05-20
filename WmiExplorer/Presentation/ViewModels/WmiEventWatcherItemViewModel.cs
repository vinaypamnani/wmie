using System;
using System.Management;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel for a single WMI event watcher item
    /// </summary>
    public class WmiEventWatcherItemViewModel : ViewModelBase, IDisposable
    {
        private readonly WmiEventWatcher _watcher;
        private readonly Action<WmiEventWatcherItemViewModel> _onRemove;
        private readonly Action<WmiEvent> _onEventReceived;
        private readonly string _eventType;
        private bool _disposed;

        /// <summary>
        /// Gets the name of the watcher
        /// </summary>
        public string Name => _watcher.Name;

        /// <summary>
        /// Gets the WQL query used by this watcher
        /// </summary>
        public string Query => _watcher.Query;

        /// <summary>
        /// Gets the namespace path this watcher is monitoring
        /// </summary>
        public string Namespace => _watcher.Namespace;

        /// <summary>
        /// Gets whether the watcher is currently running
        /// </summary>
        public bool IsRunning => _watcher.IsRunning;

        /// <summary>
        /// Gets when this watcher was created
        /// </summary>
        public DateTime CreatedAt => _watcher.CreatedAt;

        /// <summary>
        /// Gets the command to start the watcher
        /// </summary>
        public ICommand StartCommand { get; }

        /// <summary>
        /// Gets the command to stop the watcher
        /// </summary>
        public ICommand StopCommand { get; }

        /// <summary>
        /// Gets the command to remove the watcher
        /// </summary>
        public ICommand RemoveCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WmiEventWatcherItemViewModel"/> class.
        /// </summary>
        public WmiEventWatcherItemViewModel(
            WmiEventWatcher watcher,
            Action<WmiEventWatcherItemViewModel> onRemove,
            Action<WmiEvent> onEventReceived,
            string eventType)
        {
            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
            _onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
            _onEventReceived = onEventReceived ?? throw new ArgumentNullException(nameof(onEventReceived));
            _eventType = eventType ?? throw new ArgumentNullException(nameof(eventType));

            StartCommand = new RelayCommand(_ => Start(), _ => !IsRunning);
            StopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
            RemoveCommand = new RelayCommand(_ => Remove());

            _watcher.EventArrived += OnEventArrived;
        }

        private void Start()
        {
            try
            {
                _watcher.Start();
                OnPropertyChanged(nameof(IsRunning));
            }
            catch (Exception)
            {
                // Optionally log or handle error
            }
        }

        private void Stop()
        {
            try
            {
                _watcher.Stop();
                OnPropertyChanged(nameof(IsRunning));
            }
            catch (Exception)
            {
                // Optionally log or handle error
            }
        }

        private void Remove()
        {
            _onRemove(this);
        }

        private void OnEventArrived(object? sender, ManagementBaseObject e)
        {
            var actualEventType = e.ClassPath?.ClassName;
            if (!string.IsNullOrEmpty(_eventType) && !string.IsNullOrEmpty(actualEventType) && !string.Equals(_eventType, actualEventType, StringComparison.Ordinal))
            {
                // Ignore events that do not match the expected type
                return;
            }
            var wmiEvent = new WmiEvent(Name, e);
            _onEventReceived(wmiEvent);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _watcher.EventArrived -= OnEventArrived;
                _watcher.Dispose();
                _disposed = true;
            }
        }
    }
}
