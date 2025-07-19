using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;
using WmiExplorer.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
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

    // Dictionary to store state per namespace
    private readonly Dictionary<string, QueryNamespaceState> _namespaceStates = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteQueryCommand))]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private WmiInstance? _selectedResult;

    [ObservableProperty]
    private TabStatus _tabStatus;

    [ObservableProperty]
    private bool _useAmendedQualifiers = false;

    private readonly IWmiService _wmiService;

    public QueryTabViewModel(
              IMessengerService messengerService,
              IWmiService wmiService,
              ICacheService cacheService,
              SelectionManager selectionManager) : base(messengerService, selectionManager)
    {
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        // Initialize tab status with messenger service
        _tabStatus = new TabStatus(messengerService, AppState.Ready, "Enter a query and click Execute.", "Query for WMI objects");

        // Update columns when results change
        _results.CollectionChanged += (s, e) => UpdateResultColumns();
    }

    public ObservableCollection<DataGridColumn> ResultColumns { get; } = new();

    /// <summary>
    /// Gets the header text for the Query tab with count
    /// </summary>
    public string TabHeader
    {
        get
        {
            var filteredCount = Results.Count;
            if (filteredCount > 0)
            {
                return $"Query [{filteredCount}]";
            }
            return "Query";
        }
    }

    /// <summary>
    /// Clears all namespace states (useful for cleanup)
    /// </summary>
    public void ClearAllNamespaceStates()
    {
        foreach (var state in _namespaceStates.Values)
        {
            DisposeResults(state.Results);
        }
        _namespaceStates.Clear();
    }

    /// <summary>
    /// Clears namespace states for a specific namespace and all its children
    /// </summary>
    /// <param name="namespacePath">The root namespace path to clear</param>
    public void ClearNamespaceStatesForPath(string namespacePath)
    {
        if (string.IsNullOrEmpty(namespacePath))
            return;

        var pathsToRemove = new List<string>();

        // Find all namespace paths that start with the given path (including children)
        foreach (var kvp in _namespaceStates)
        {
            if (kvp.Key.StartsWith(namespacePath, StringComparison.OrdinalIgnoreCase))
            {
                pathsToRemove.Add(kvp.Key);
            }
        }

        // Remove the found paths and dispose their results
        foreach (var path in pathsToRemove)
        {
            if (_namespaceStates.TryGetValue(path, out var state))
            {
                DisposeResults(state.Results);
                _namespaceStates.Remove(path);
            }
        }

        // If we cleared the current namespace, also clear current results
        if (SelectionManager.SelectedNamespace?.NamespacePath != null &&
            SelectionManager.SelectedNamespace.NamespacePath.StartsWith(namespacePath, StringComparison.OrdinalIgnoreCase))
        {
            ClearCurrentState();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clear all namespace states
            ClearAllNamespaceStates();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Called after results are updated. Re-subscribes to the new collection view and updates tab header.
    /// </summary>
    protected override void OnResultsUpdated()
    {
        // Immediately update tab header since results just changed
        OnPropertyChanged(nameof(TabHeader));
    }

    /// <summary>
    /// Called when the selected namespace changes. Override from SelectionAwareViewModelBase.
    /// Restores state for the new namespace.
    /// </summary>
    protected override void OnSelectedNamespaceChanged(WmiNamespaceViewModel? selectedNamespace)
    {
        // Restore state for the new namespace
        RestoreNamespaceState(selectedNamespace);

        // Update tab header
        OnPropertyChanged(nameof(TabHeader));
    }

    /// <summary>
    /// Called before the selected namespace changes. Override from SelectionAwareViewModelBase.
    /// Saves state for the current namespace before it changes.
    /// </summary>
    protected override void OnSelectedNamespaceChanging(WmiNamespaceViewModel? currentNamespace)
    {
        // Save state for the current namespace before it changes
        SaveCurrentNamespaceState(currentNamespace);
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

    private void ClearCurrentState()
    {
        QueryText = string.Empty;
        DirectRead = false;
        UseAmendedQualifiers = true;
        _results.Clear();
        UpdateResultColumns();
    }

    /// <summary>
    /// Command to clear query results
    /// </summary>
    [RelayCommand]
    private void ClearResults()
    {
        _results.Clear();
        DisposeResults(_results);
        RefreshResultsView();
        UpdateResultColumns();
        SelectionManager.PropertyGrid.ClearPropertyGrid();
    }

    private void DisposeResults(IEnumerable<WmiInstance> results)
    {
        foreach (var instance in results)
        {
            instance.Dispose();
        }
    }

    /// <summary>
    /// Command to execute a WMI query
    /// </summary>
    [RelayCommand(CanExecute = nameof(ExecuteQueryCanExecute))]
    private async Task ExecuteQueryAsync()
    {
        IsQuerying = true;
        TabStatus.SetBusy("Executing query...");

        var tempResults = new List<WmiInstance>();

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        using var timer = OperationTimer.Start($"Executing query: {QueryText}", _messengerService);

        try
        {
            if (SelectionManager.SelectedNamespace == null)
            {
                TabStatus.SetError("No namespace selected.");
                return;
            }

            if (string.IsNullOrWhiteSpace(QueryText))
            {
                TabStatus.SetError("Query text is empty.");
                return;
            }

            var scope = SelectionManager.SelectedNamespace.ManagementScope;
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
                    Log.Warning(ex, "Error converting ManagementObject to WmiInstance, skipping object");
                }
            }

            // Update results and related helpers
            SetResults(tempResults);

            // Update columns only once, after all results are loaded
            UpdateResultColumns();

            if (_results.Count == 0)
            {
                TabStatus.SetWarning("No results returned.");
            }
            else if (token.IsCancellationRequested)
            {
                TabStatus.SetWarning($"Query cancelled. Found {_results.Count} results before cancellation.");
            }
            else
            {
                TabStatus.SetSuccess($"Query returned {_results.Count} result(s).");
            }
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Query cancelled. Found {Count} results before cancellation.", _results.Count);
            TabStatus.SetWarning($"Query cancelled. Found {_results.Count} results before cancellation.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WMI query execution failed: {QueryText}", QueryText);
            TabStatus.SetError($"Query failed: {ex.Message}", ex);
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

    private void RefreshResultsView()
    {
        _resultsView?.Refresh();
    }

    private void RestoreNamespaceState(WmiNamespaceViewModel? selectedNamespace)
    {
        if (selectedNamespace?.NamespacePath == null)
        {
            // Clear everything if no namespace selected
            ClearCurrentState();
            return;
        }

        if (_namespaceStates.TryGetValue(selectedNamespace.NamespacePath, out var state))
        {
            // Restore state for this namespace
            QueryText = state.QueryText;
            DirectRead = state.DirectRead;
            UseAmendedQualifiers = state.UseAmendedQualifiers;

            // Restore results
            _results.Clear();
            foreach (var result in state.Results)
            {
                _results.Add(result);
            }
            UpdateResultColumns();
        }
        else
        {
            // New namespace - clear everything
            ClearCurrentState();
        }
    }

    private void SaveCurrentNamespaceState(WmiNamespaceViewModel? namespaceViewModel = null)
    {
        // Use provided namespace or fall back to previous namespace from SelectionManager
        var namespaceToSave = namespaceViewModel ?? SelectionManager.PreviousNamespace;
        if (namespaceToSave?.NamespacePath == null) return;

        var state = new QueryNamespaceState
        {
            QueryText = QueryText,
            DirectRead = DirectRead,
            UseAmendedQualifiers = UseAmendedQualifiers,
            Results = new List<WmiInstance>(_results)
        };

        _namespaceStates[namespaceToSave.NamespacePath] = state;
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
                Binding = new MultiBinding
                {
                    Converter = (IMultiValueConverter)Application.Current.FindResource("SafePropertyValueConverter"),
                    Bindings =
                    {
                        new Binding(), // The WmiInstance itself (DataContext of row)
                        new Binding { Source = property.Name }
                    }
                },
                SortMemberPath = property.Name
            };
            ResultColumns.Add(column);
        }
    }

    private class QueryNamespaceState
    {
        public bool DirectRead { get; set; } = false;
        public string QueryText { get; set; } = string.Empty;
        public List<WmiInstance> Results { get; set; } = new();
        public bool UseAmendedQualifiers { get; set; } = true;
    }
}