using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Messages;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// ViewModel for WMI query execution and result management.
/// Handles query operations, filtering, and result display.
/// </summary>
public partial class QueryTabViewModel : ResultsViewModelBase<WmiInstance>
{
    [ObservableProperty]
    private ICacheService _cacheService;

    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _directRead = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteQueryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelQueryCommand))]
    private bool _isQuerying;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteQueryCommand))]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private WmiNamespaceViewModel? _selectedNamespace;

    [ObservableProperty]
    private WmiInstance? _selectedResult;

    [ObservableProperty]
    private bool _useAmendedQualifiers = true;

    private readonly IWmiService _wmiService;

    public QueryTabViewModel(IMessengerService messengerService, IWmiService wmiService, ICacheService cacheService)
                 : base(messengerService)
    {
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        // Subscribe to namespace selection changes
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);

        // Update columns when results change
        _results.CollectionChanged += (s, e) => UpdateResultColumns();
    }

    public ObservableCollection<DataGridColumn> ResultColumns { get; } = new();

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

    /// <summary>
    /// Command to cancel the current query operation
    /// </summary>
    [RelayCommand(CanExecute = nameof(CancelQueryCanExecute))]
    private void CancelQuery()
    {
        if (!IsQuerying || _cts == null)
            return;

        PublishBusyState("Cancelling query...");
        _cts.Cancel();
    }

    /// <summary>
    /// Determines if the query can be cancelled
    /// </summary>
    private bool CancelQueryCanExecute()
    {
        return IsQuerying;
    }

    /// <summary>
    /// Command to clear query results
    /// </summary>
    [RelayCommand]
    private void ClearResults()
    {
        _results.Clear();
        RefreshResultsView();
        UpdateResultColumns();
    }

    /// <summary>
    /// Command to execute a WMI query
    /// </summary>
    [RelayCommand(CanExecute = nameof(ExecuteQueryCanExecute))]
    private async Task ExecuteQueryAsync()
    {
        IsQuerying = true;
        PublishBusyState("Executing query...");

        var tempResults = new List<WmiInstance>();

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        using var timer = OperationTimer.Start($"Executing query: {QueryText}", _messengerService);

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
                DirectRead,
                UseAmendedQualifiers,
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
        }
    }

    /// <summary>
    /// Determines if the query can be executed
    /// </summary>
    private bool ExecuteQueryCanExecute()
    {
        return !string.IsNullOrWhiteSpace(QueryText) && !IsQuerying;
    }

    private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
    {
        if (message?.NamespaceViewModel == null)
            return;
        SelectedNamespace = message.NamespaceViewModel;
    }

    /// <summary>
    /// Handles property change for SelectedResult to publish messaging updates
    /// </summary>
    partial void OnSelectedResultChanged(WmiInstance? value)
    {
        // Publish a message so MainViewModel can update SelectedObject
        PublishMessage(new WmiQueryInstanceChangedMessage(value));
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