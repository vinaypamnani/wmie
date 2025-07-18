using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// ViewModel for the logging tab
/// </summary>
public partial class LogTabViewModel : MessagingViewModelBase
{
    [ObservableProperty]
    private bool _autoScroll = true;

    private ICollectionView? _filteredLogsView;

    [ObservableProperty]
    private LogLevel _filterLogLevel;

    private readonly ObservableCollection<LogEntry> _logEntries = new();
    private readonly object _logEntriesLock = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private LogEntry? _selectedLogEntry;

    private readonly SettingsManager _settingsManager;

    [ObservableProperty]
    private TabStatus _tabStatus;

    /// <summary>
    /// Initializes a new instance of the LogTabViewModel class
    /// </summary>
    /// <param name="messengerService">The messenger service</param>
    /// <param name="settingsManager">The settings manager</param>
    public LogTabViewModel(IMessengerService messengerService, SettingsManager settingsManager)
        : base(messengerService)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));

        // Initialize filter log level from settings
        _filterLogLevel = _settingsManager.LogLevel;

        LogEntries = new ReadOnlyObservableCollection<LogEntry>(_logEntries);

        // Initialize tab status with messenger service
        _tabStatus = new TabStatus(messengerService, AppState.Ready, "Application log", "Application log");

        // Load existing log entries that were created before this ViewModel was initialized
        LoadExistingLogEntries();

        // Subscribe to global logging events for new entries
        Log.LogEntryAdded += OnLogEntryAdded;

        // Set up filtered view
        SetupFilteredView();
    }

    /// <summary>
    /// Gets the available log levels for filtering
    /// </summary>
    public LogLevel[] AvailableLogLevels { get; } = Enum.GetValues<LogLevel>();

    /// <summary>
    /// Gets the filtered view of log entries
    /// </summary>
    public ICollectionView FilteredLogsView
    {
        get
        {
            if (_filteredLogsView == null)
            {
                SetupFilteredView();
            }
            return _filteredLogsView!;
        }
    }

    /// <summary>
    /// Gets whether there are any log entries
    /// </summary>
    public bool HasLogEntries => _logEntries.Count > 0;

    /// <summary>
    /// Gets the collection of log entries
    /// </summary>
    public ReadOnlyObservableCollection<LogEntry> LogEntries { get; }

    // Add a property for direct binding if needed
    public SettingsManager SettingsManager => _settingsManager;

    /// <summary>
    /// Disposes the LogTabViewModel and cleans up resources
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from the global logging event
            Log.LogEntryAdded -= OnLogEntryAdded;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Adds a log entry to the collection (must be called on UI thread)
    /// </summary>
    /// <param name="logEntry">The log entry to add</param>
    private void AddLogEntry(LogEntry logEntry)
    {
        lock (_logEntriesLock)
        {
            _logEntries.Add(logEntry);

            // Remove old entries if we exceed the limit
            while (_logEntries.Count > Log.MaxInMemoryLogEntries)
            {
                _logEntries.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Command to clear in-memory logs
    /// </summary>
    [RelayCommand]
    private void ClearLogs()
    {
        var count = _logEntries.Count;
        lock (_logEntriesLock)
        {
            _logEntries.Clear();
        }

        Log.Information("Cleared {Count} log entries", count);
        OnPropertyChanged(nameof(HasLogEntries));
    }

    /// <summary>
    /// Loads existing log entries from the in-memory sink that were created before this ViewModel was initialized
    /// </summary>
    private void LoadExistingLogEntries()
    {
        var existingEntries = Log.GetStoredLogEntries();
        if (existingEntries?.Any() == true)
        {
            lock (_logEntriesLock)
            {
                foreach (var entry in existingEntries)
                {
                    _logEntries.Add(entry);
                }
            }
        }
    }

    /// <summary>
    /// Filter predicate for log entries
    /// </summary>
    /// <param name="obj">The log entry to filter</param>
    /// <returns>True if the entry should be displayed</returns>
    private bool LogEntryFilter(object obj)
    {
        if (obj is not LogEntry entry)
            return false;

        // Filter by log level
        if (entry.Level < FilterLogLevel)
            return false;

        // Filter by search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            return entry.Message.ToLowerInvariant().Contains(searchLower) ||
                   entry.Source.ToLowerInvariant().Contains(searchLower) ||
                   (entry.ExceptionText?.ToLowerInvariant().Contains(searchLower) == true);
        }

        return true;
    }

    /// <summary>
    /// Called when FilterLogLevel property changes
    /// </summary>
    partial void OnFilterLogLevelChanged(LogLevel value)
    {
        FilteredLogsView.Refresh();
    }

    /// <summary>
    /// Handles new log entries from the global logging system
    /// </summary>
    /// <param name="logEntry">The log entry that was added</param>
    private void OnLogEntryAdded(LogEntry logEntry)
    {
        if (logEntry == null)
            return;

        // No need to filter here anymore - Serilog handles global minimum level filtering
        // Ensure UI updates happen on the UI thread
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == false)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => AddLogEntry(logEntry));
        }
        else
        {
            AddLogEntry(logEntry);
        }
    }

    /// <summary>
    /// Called when SearchText property changes
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        FilteredLogsView.Refresh();
    }

    /// <summary>
    /// Command to open the log file location
    /// </summary>
    [RelayCommand]
    private void OpenLogFileLocation()
    {
        try
        {
            var logDirectory = Path.GetDirectoryName(Log.LogFilePath);
            if (!string.IsNullOrEmpty(logDirectory) && Directory.Exists(logDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", logDirectory);
            }
            else
            {
                PublishWarningState("Log directory not found");
            }
        }
        catch (Exception ex)
        {
            PublishErrorState($"Failed to open log location: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets up the filtered collection view
    /// </summary>
    private void SetupFilteredView()
    {
        _filteredLogsView = CollectionViewSource.GetDefaultView(_logEntries);
        _filteredLogsView.Filter = LogEntryFilter;

        // Monitor collection changes
        _logEntries.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasLogEntries));

            // Send message that tab count changed
            PublishMessage(new TabCountChangedMessage());

            // Auto-scroll to the latest entry if enabled
            if (AutoScroll && _logEntries.Count > 0)
            {
                SelectedLogEntry = _logEntries[^1];
            }
        };
    }
}