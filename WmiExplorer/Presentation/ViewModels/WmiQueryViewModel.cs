using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModelHelpers;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels;

public class WmiQueryViewModel : ResultsViewModelBase<WmiInstance>
{
    private readonly ICacheService _cacheService;
    private CancellationTokenSource? _cts;
    private bool _directRead = false;
    private bool _isQuerying;
    private readonly IMessagingService _messagingService;
    private string _queryText = string.Empty;
    private WmiNamespaceViewModel? _selectedNamespace;
    private WmiInstance? _selectedResult;
    private bool _useAmendedQualifiers = true;
    private readonly IWmiService _wmiService;

    public WmiQueryViewModel(IMessagingService messagingService, IWmiService wmiService, ICacheService cacheService)
        : base()
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        // Initialize messaging
        InitializeMessaging(messagingService);

        // Initialize commands
        ExecuteQueryCommand = new AsyncRelayCommand(ExecuteQueryAsync, CanExecuteQuery);
        ClearResultsCommand = new RelayCommand(_ => ClearResults());
        CancelQueryCommand = new RelayCommand(_ => CancelQuery(), _ => IsQuerying);

        // Subscribe to namespace selection changes
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);

        // Update columns when results change
        _results.CollectionChanged += (s, e) => UpdateResultColumns();
    }

    public ICacheService CacheService => _cacheService;
    public ICommand CancelQueryCommand { get; }
    public ICommand ClearResultsCommand { get; }

    public bool DirectRead
    {
        get => _directRead;
        set => SetProperty(ref _directRead, value);
    }

    public ICommand ExecuteQueryCommand { get; }

    public string QueryText
    {
        get => _queryText;
        set
        {
            if (SetProperty(ref _queryText, value))
            {
                // Refresh the command's CanExecute state
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ObservableCollection<DataGridColumn> ResultColumns { get; } = new();

    public WmiNamespaceViewModel? SelectedNamespace
    {
        get => _selectedNamespace;
        set => SetProperty(ref _selectedNamespace, value);
    }

    public WmiInstance? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetProperty(ref _selectedResult, value))
            {
                // Publish a message so MainViewModel can update SelectedObject
                _messagingService.Publish(new WmiQueryInstanceChangedMessage(value));
            }
        }
    }

    public bool UseAmendedQualifiers
    {
        get => _useAmendedQualifiers;
        set => SetProperty(ref _useAmendedQualifiers, value);
    }

    public bool IsQuerying
    {
        get => _isQuerying;
        set => SetProperty(ref _isQuerying, value);
    }

    private void CancelQuery()
    {
        if (!IsQuerying || _cts == null)
            return;

        PublishBusyState("Cancelling query...");
        _cts.Cancel();
    }

    private bool CanExecuteQuery()
    {
        return !string.IsNullOrWhiteSpace(QueryText) && !_isQuerying;
    }

    private void ClearResults()
    {
        _results.Clear();
        RefreshResultsView();
        UpdateResultColumns();
    }

    private async Task ExecuteQueryAsync()
    {
        IsQuerying = true;
        PublishBusyState("Executing query...");

        var tempResults = new List<WmiInstance>();

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        using var timer = OperationTimer.Start($"Executing query: {_queryText}", MessageService!);

        try
        {
            if (SelectedNamespace == null)
            {
                PublishErrorState("No namespace selected.");
                return;
            }

            if (string.IsNullOrWhiteSpace(QueryText))
            {
                PublishErrorState("Query text is empty.");
                return;
            }

            var scope = SelectedNamespace.ManagementScope;
            var queryString = QueryText.Trim();
            var managementObjects = await _wmiService.ExecuteWmiQueryAsync(
                scope,
                queryString,
                _directRead,
                _useAmendedQualifiers,
                cancellationToken: token);

            foreach (var mo in managementObjects)
            {
                try
                {
                    tempResults.Add(new WmiInstance(mo));
                }
                catch (Exception ex)
                {
                    // Log and skip invalid objects
                    System.Diagnostics.Debug.WriteLine($"Error converting ManagementObject to WmiInstance: {ex.Message}");
                }
            }

            // Use the base class method to update results and related helpers
            SetResults(tempResults);

            // Update columns only once, after all results are loaded
            UpdateResultColumns();

            if (_results.Count == 0)
            {
                PublishWarningState("No results returned.");
            }
            else if (token.IsCancellationRequested)
            {
                PublishWarningState($"Query cancelled. Found {_results.Count} results before cancellation.");
            }
            else
            {
                PublishSuccessState($"Query returned {_results.Count} result(s).");
            }
        }
        catch (OperationCanceledException)
        {
            PublishWarningState($"Query cancelled. Found {_results.Count} results before cancellation.");
        }
        catch (Exception ex)
        {
            PublishErrorState($"Query failed: {ex.Message}");
        }
        finally
        {
            IsQuerying = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
    {
        if (message?.NamespaceViewModel == null)
            return;
        SelectedNamespace = message.NamespaceViewModel;
    }

    protected override bool ResultsFilterPredicate(WmiInstance instance, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;
        var lower = filter.ToLowerInvariant();

        // Helper to safely access a property for filtering
        bool SafeContains(Func<string?> propertyAccessor)
        {
            try
            {
                var value = propertyAccessor();
                return value != null && value.ToLowerInvariant().Contains(lower);
            }
            catch
            {
                // Ignore exceptions from WMI property access
                return false;
            }
        }

        // Match on multiple properties, safely
        return SafeContains(() => instance.InstanceName)
            || SafeContains(() => instance.Path.Path)
            || SafeContains(() => instance.ClassPath?.ClassName)
            || SafeContains(() => instance.ToString());
    }

    private void RefreshResultsView()
    {
        _resultsView?.Refresh();
    }

    private void UpdateResultColumns()
    {
        ResultColumns.Clear();

        // Use the first result to determine property names for columns
        var firstInstance = _results.FirstOrDefault();
        if (firstInstance == null)
            return;

        var propertyDataCollection = firstInstance.Properties as System.Management.PropertyDataCollection;
        if (propertyDataCollection == null)
            return;

        foreach (var property in propertyDataCollection.Cast<System.Management.PropertyData>())
        {
            var column = new DataGridTextColumn
            {
                Header = property.Name,
                Binding = new Binding($"Properties[{property.Name}].Value"),
                SortMemberPath = $"Properties[{property.Name}].Value"
            };
            ResultColumns.Add(column);
        }
    }
}