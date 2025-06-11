using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using WmiExplorer.Common.Base;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Watcher;

/// <summary>
/// Manages collections of WMI event and target classes with filtering support
/// </summary>
public class ClassListManager : DisposableObservableObject
{
    private readonly ICacheService _cacheService;
    private readonly string _defaultEventClass = "__InstanceCreationEvent";
    private readonly ObservableCollection<string> _eventClasses = new();
    private readonly ObservableCollection<string> _targetClasses = new();
    private readonly FilterHelper<string> _targetClassFilter;

    /// <summary>
    /// Initializes a new instance of the ClassListManager class
    /// </summary>
    /// <param name="cacheService">The cache service for loading classes</param>
    public ClassListManager(ICacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        _targetClassFilter = TrackDisposable(new FilterHelper<string>(_targetClasses,
            (className, filter) => string.IsNullOrEmpty(filter) ||
                className.Contains(filter, StringComparison.OrdinalIgnoreCase)));

        EventClasses = new ReadOnlyObservableCollection<string>(_eventClasses);
        TargetClasses = new ReadOnlyObservableCollection<string>(_targetClasses);
        EventClassListView = CollectionViewSource.GetDefaultView(EventClasses);
    }

    /// <summary>
    /// Gets the read-only collection of event classes
    /// </summary>
    public ReadOnlyObservableCollection<string> EventClasses { get; }

    /// <summary>
    /// Gets the collection view for event classes
    /// </summary>
    public ICollectionView EventClassListView { get; }

    /// <summary>
    /// Gets the read-only collection of target classes
    /// </summary>
    public ReadOnlyObservableCollection<string> TargetClasses { get; }

    /// <summary>
    /// Gets or sets the filter text for target classes
    /// </summary>
    public string TargetClassFilter
    {
        get => _targetClassFilter.FilterText;
        set => _targetClassFilter.FilterText = value;
    }

    /// <summary>
    /// Gets the filtered collection view for target classes
    /// </summary>
    public ICollectionView TargetClassListView => _targetClassFilter.CollectionView;

    /// <summary>
    /// Clears all class collections
    /// </summary>
    public void Clear()
    {
        _eventClasses.Clear();
        _targetClasses.Clear();
    }

    /// <summary>
    /// Checks if the specified target class exists in the collection
    /// </summary>
    /// <param name="targetClass">The target class name to check</param>
    /// <returns>True if the target class exists in the collection</returns>
    public bool ContainsTargetClass(string targetClass)
    {
        return !string.IsNullOrEmpty(targetClass) && _targetClasses.Contains(targetClass);
    }

    /// <summary>
    /// Gets the default event class name if it exists in the collection
    /// </summary>
    /// <param name="defaultEventClass">The default event class name to look for</param>
    /// <returns>The default event class if found, otherwise the first event class, or null if none exist</returns>
    public string? GetDefaultOrFirstEventClass()
    {
        if (_eventClasses.Contains(_defaultEventClass))
            return _defaultEventClass;

        return _eventClasses.FirstOrDefault();
    }

    /// <summary>
    /// Updates both event and target class lists for the specified namespace
    /// </summary>
    /// <param name="selectedNamespace">The selected namespace view model</param>
    /// <returns>A task representing the async operation</returns>
    public async Task UpdateClassListsAsync(WmiNamespaceViewModel? selectedNamespace)
    {
        await Task.WhenAll(
            UpdateEventClassesAsync(selectedNamespace),
            UpdateTargetClassesAsync(selectedNamespace)
        );
    }

    /// <summary>
    /// Updates the event classes collection
    /// </summary>
    /// <param name="selectedNamespace">The selected namespace view model</param>
    /// <returns>A task representing the async operation</returns>
    public async Task UpdateEventClassesAsync(WmiNamespaceViewModel? selectedNamespace)
    {
        await PopulateClassListAsync(true, _eventClasses, selectedNamespace);
        EventClassListView.Refresh();
    }

    /// <summary>
    /// Updates the target classes collection
    /// </summary>
    /// <param name="selectedNamespace">The selected namespace view model</param>
    /// <returns>A task representing the async operation</returns>
    public async Task UpdateTargetClassesAsync(WmiNamespaceViewModel? selectedNamespace)
    {
        await PopulateClassListAsync(false, _targetClasses, selectedNamespace);
        TargetClassListView.Refresh();
    }

    /// <summary>
    /// Disposes the manager and clears all collections
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Clear();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Gets the default event class names as a fallback
    /// </summary>
    /// <returns>A collection of default event class names</returns>
    private static IEnumerable<string> GetDefaultEventClasses()
    {
        return new[]
        {
            "__InstanceCreationEvent",
            "__InstanceModificationEvent",
            "__InstanceDeletionEvent",
            "__InstanceOperationEvent",
            "__ClassCreationEvent",
            "__ClassModificationEvent",
            "__ClassDeletionEvent",
        };
    }

    /// <summary>
    /// Populates a class list collection with event or target classes
    /// </summary>
    /// <param name="eventClassesOnly">True to load event classes, false for target classes</param>
    /// <param name="targetCollection">The collection to populate</param>
    /// <param name="selectedNamespace">The selected namespace view model</param>
    /// <returns>A task representing the async operation</returns>
    private async Task PopulateClassListAsync(bool eventClassesOnly, ObservableCollection<string> targetCollection, WmiNamespaceViewModel? selectedNamespace)
    {
        targetCollection.Clear();
        IEnumerable<string> classNames = Enumerable.Empty<string>();

        if (selectedNamespace != null)
        {
            // Prefer in-memory classes if available
            var inMemoryClassesLoaded = selectedNamespace.ClassLoadState == ClassLoadState.Success;

            if (inMemoryClassesLoaded)
            {
                var inMemoryClasses = selectedNamespace.Classes?
                .Where(c => eventClassesOnly ? c.IsEventClass : !c.IsEventClass)
                .Select(c => c.ClassName)
                .ToList();

                classNames = inMemoryClasses ?? Enumerable.Empty<string>();
            }
            else
            {
                try
                {
                    var cachedClasses = await _cacheService.GetClassesForNamespaceAsync(selectedNamespace.NamespacePath);
                    if (cachedClasses.Count > 0)
                    {
                        classNames = cachedClasses
                            .Where(c => eventClassesOnly ? c.IsEventClass : !c.IsEventClass)
                            .Select(c => c.ClassName);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ClassListManager] Cache error: {ex.Message}");
                }
            }
        }

        // For event classes, provide fallback defaults if no classes found
        if (eventClassesOnly && !classNames.Any())
        {
            classNames = GetDefaultEventClasses();
        }

        // Sort: system classes (names starting with "__") first, then others, both groups sorted ascending (A-Z)
        var systemClasses = classNames.Where(n => n.StartsWith("__")).Distinct().OrderBy(n => n, StringComparer.Ordinal);
        var userClasses = classNames.Where(n => !n.StartsWith("__")).Distinct().OrderBy(n => n, StringComparer.Ordinal);

        foreach (var name in systemClasses.Concat(userClasses))
            targetCollection.Add(name);
    }
}