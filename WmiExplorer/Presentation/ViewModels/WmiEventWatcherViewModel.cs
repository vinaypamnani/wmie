using System.Collections.ObjectModel;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.ViewModels
{
    /// <summary>
    /// View model for WMI Event Watcher tab
    /// </summary>
    public class WmiEventWatcherViewModel : ViewModelBase
    {
        private string _query = string.Empty;
        private string _namespace = "root\\CIMV2";
        private string _selectedEventType = "All Events";
        private bool _isWatching;

        /// <summary>
        /// Gets or sets the WMI query for watching events
        /// </summary>
        public string Query
        {
            get => _query;
            set
            {
                if (_query != value)
                {
                    _query = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the WMI namespace to watch
        /// </summary>
        public string Namespace
        {
            get => _namespace;
            set
            {
                if (_namespace != value)
                {
                    _namespace = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected event type
        /// </summary>
        public string SelectedEventType
        {
            get => _selectedEventType;
            set
            {
                if (_selectedEventType != value)
                {
                    _selectedEventType = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the watcher is currently active
        /// </summary>
        public bool IsWatching
        {
            get => _isWatching;
            set
            {
                if (_isWatching != value)
                {
                    _isWatching = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(WatchButtonText));
                }
            }
        }

        /// <summary>
        /// Gets the text for the watch button based on current state
        /// </summary>
        public string WatchButtonText => IsWatching ? "Stop Watching" : "Start Watching";

        /// <summary>
        /// Gets the collection of available event types
        /// </summary>
        public ObservableCollection<string> EventTypes { get; } = new ObservableCollection<string>
        {
            "All Events",
            "Creation Events",
            "Modification Events",
            "Deletion Events",
            "Custom Events"
        };

        /// <summary>
        /// Gets the collection of captured events
        /// </summary>
        public ObservableCollection<object> CapturedEvents { get; } = new ObservableCollection<object>();

        /// <summary>
        /// Gets the command to toggle watching state
        /// </summary>
        public ICommand ToggleWatchCommand { get; }

        /// <summary>
        /// Gets the command to clear captured events
        /// </summary>
        public ICommand ClearEventsCommand { get; }

        /// <summary>
        /// Gets the collection of intrinsic event types
        /// </summary>
        public ObservableCollection<string> IntrinsicEventTypes { get; } = new ObservableCollection<string>
        {
            "__InstanceCreationEvent",
            "__InstanceModificationEvent",
            "__InstanceDeletionEvent",
            "__InstanceOperationEvent",
            "__ClassCreationEvent",
            "__ClassModificationEvent",
            "__ClassDeletionEvent"
        };

        /// <summary>
        /// Gets the collection of target classes
        /// </summary>
        public ObservableCollection<string> TargetClasses { get; } = new ObservableCollection<string>
        {
            "Win32_Process",
            "Win32_Service",
            "Win32_LogicalDisk"
        };

        /// <summary>
        /// Gets the collection of watchers
        /// </summary>
        public ObservableCollection<object> Watchers { get; } = new ObservableCollection<object>
        {
            new { Status = "Running", EventType = "__InstanceCreationEvent", ClassName = "Win32_Process" },
            new { Status = "Stopped", EventType = "__InstanceModificationEvent", ClassName = "Win32_Service" }
        };

        /// <summary>
        /// Gets the collection of running watchers
        /// </summary>
        public ObservableCollection<object> RunningWatchers { get; } = new ObservableCollection<object>
        {
            new { Name = "Watcher 1" },
            new { Name = "Watcher 2" }
        };

        /// <summary>
        /// Gets the collection of event results
        /// </summary>
        public ObservableCollection<object> EventResults { get; } = new ObservableCollection<object>
        {
            new { Timestamp = "2025-05-18 10:00:00", EventType = "__InstanceCreationEvent" },
            new { Timestamp = "2025-05-18 10:01:00", EventType = "__InstanceModificationEvent" }
        };

        public WmiEventWatcherViewModel()
        {
            // Commands would be implemented here
            // This is just UI design for now, so we won't implement the actual commands
            ToggleWatchCommand = new RelayCommand(_ => IsWatching = !IsWatching);
            ClearEventsCommand = new RelayCommand(_ => CapturedEvents.Clear());
        }
    }
}
