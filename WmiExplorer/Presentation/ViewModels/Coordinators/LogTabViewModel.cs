using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Models;
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
    private LogLevel _minimumLogLevel;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private LogEntry? _selectedLogEntry;

    private readonly ISettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the LogTabViewModel class
    /// </summary>
    /// <param name="messengerService">The messenger service</param>
    /// <param name="settingsService">The settings service</param>
    public LogTabViewModel(IMessengerService messengerService, ISettingsService settingsService)
        : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Initialize minimum log level from settings
        _minimumLogLevel = _settingsService.LogLevel;
        _filterLogLevel = _minimumLogLevel;

        LogEntries = new ReadOnlyObservableCollection<LogEntry>(_logEntries);

        // Subscribe to global logging events
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

    /// <summary>
    /// Gets the current log file path
    /// </summary>
    public string LogFilePath
    {
        get
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WmiExplorer", "logs");
            return Path.Combine(logDirectory, "wmi-explorer.log");
        }
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
            while (_logEntries.Count > 1000)
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
                   (entry.Exception?.ToLowerInvariant().Contains(searchLower) == true);
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
    /// Called when MinimumLogLevel property changes
    /// </summary>
    partial void OnMinimumLogLevelChanged(LogLevel value)
    {
        // Update Serilog's global minimum level
        Log.SetMinimumLevel(value);

        // Update the setting value
        _settingsService.LogLevel = value;
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
            var logDirectory = Path.GetDirectoryName(LogFilePath);
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

            // Auto-scroll to the latest entry if enabled
            if (AutoScroll && _logEntries.Count > 0)
            {
                SelectedLogEntry = _logEntries[^1];
            }
        };
    }
}