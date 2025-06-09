using System.Collections.ObjectModel;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Namespaces pane. Manages the collection of namespaces
/// and related UI operations for the namespace tree view.
/// </summary>
public class WmiNamespacePaneViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;
    private readonly ICacheService _cacheService;
    private readonly WmiClassesTabViewModel _classesTabViewModel;
    private readonly CancellationTokenSource _cts = new();
    private WmiNamespaceViewModel? _selectedNamespace;
    private readonly ISettingsService _settingsService;
    private MainWindowPosition _windowPosition;
    private readonly IWmiService _wmiService;
    private readonly WmiWatcherViewModel _watcherViewModel;

    public WmiNamespacePaneViewModel(
        IMessagingService messagingService,
        ISettingsService settingsService,
        IWmiService wmiService,
        IApplicationService applicationService,
        ICacheService cacheService,
        WmiClassesTabViewModel classesTabViewModel,
        WmiWatcherViewModel watcherViewModel)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _classesTabViewModel = classesTabViewModel ?? throw new ArgumentNullException(nameof(classesTabViewModel));
        _watcherViewModel = watcherViewModel ?? throw new ArgumentNullException(nameof(watcherViewModel));

        // Initialize messaging
        InitializeMessaging(messagingService);

        // Subscribe to messages
        StrongSubscribe<JumpToClassMessage>(HandleJumpToClassMessage);
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
        StrongSubscribe<ClassesFilteredMessage>(HandleClassesFilteredMessage);

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;
    }

    /// <summary>
    /// Gets the WmiClassesTabViewModel
    /// </summary>
    public WmiClassesTabViewModel ClassesTabViewModel => _classesTabViewModel;

    /// <summary>
    /// Gets the view model for the WMI Event Watcher
    /// </summary>
    public WmiWatcherViewModel WatcherViewModel => _watcherViewModel;

    /// <summary>
    /// Collection of WMI namespaces in the tree
    /// </summary>
    public ObservableCollection<WmiNamespaceViewModel> Namespaces { get; } = new();

    /// <summary>
    /// Command to reload the classes of the selected namespace
    /// </summary>
    public ICommand ReloadClassesCommand => new RelayCommand(
        _ => SelectedNamespace?.LoadClassesCommand.Execute(null),
        _ => SelectedNamespace != null && SelectedNamespace.LoadClassesCommand.CanExecute(null)
    );

    /// <summary>
    /// Currently selected namespace in the tree
    /// </summary>
    public WmiNamespaceViewModel? SelectedNamespace
    {
        get => _selectedNamespace;
        set
        {
            if (SetProperty(ref _selectedNamespace, value) && value != null)
            {
                // Make sure selected namespaces are expanded
                if (!value.IsExpanded)
                {
                    value.IsExpanded = true;
                }

                // Publish message about the selected namespace change
                PublishMessage(new SelectedNamespaceChangedMessage(value));
            }
        }
    }

    /// <summary>
    /// Gets the window position settings
    /// </summary>
    public MainWindowPosition WindowPosition
    {
        get => _windowPosition;
        set => SetProperty(ref _windowPosition, value);
    }

    /// <summary>
    /// Connects to the specified computer or namespace path
    /// </summary>
    public async Task ConnectAsync(string computerOrNamespacePath)
    {
        // Parse the input to determine what type of connection we're making
        string effectivePath;

        try
        {
            // Normalize the input and determine the path type
            if (string.IsNullOrEmpty(computerOrNamespacePath) || computerOrNamespacePath == "." || computerOrNamespacePath.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Case 1: Local machine - display as \\COMPUTERNAME\ROOT
                effectivePath = @"\\.\ROOT";
            }
            else if (computerOrNamespacePath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
            {
                // Case 4: Full WMI path with computer - use as is
                // Format: \\computer\namespace (e.g., \\computer\root\cimv2)
                effectivePath = computerOrNamespacePath;
            }
            else if (computerOrNamespacePath.Contains("\\"))
            {
                // Case 3: Namespace path without computer - assume local computer
                // Format: root\namespace (e.g., root\cimv2)
                effectivePath = $@"\\.\{computerOrNamespacePath}";
            }
            else
            {
                // Case 2: Computer name only - display as \\COMPUTERNAME\ROOT
                effectivePath = $@"\\{computerOrNamespacePath}\ROOT";
            }

            PublishBusyState($"Connecting to {effectivePath}...");

            // Check if we're already connected to this path
            var existingRoot = Namespaces.FirstOrDefault(n =>
                n.NamespacePath.Equals(effectivePath, StringComparison.OrdinalIgnoreCase));

            if (existingRoot != null)
            {
                // Just select the existing root namespace
                SelectedNamespace = existingRoot;
                PublishSuccessState($"Connected to {effectivePath}");
                return;
            }

            // Force garbage collection to release any existing WMI resources
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Create the root namespace view model using the async method
            var rootViewModel = await WmiNamespaceViewModel.CreateRootAsync(
                effectivePath,
                _wmiService,
                MessageService!,
                _applicationService,
                _settingsService,
                _cacheService,
                _cts.Token);

            // Load initial children
            await rootViewModel.ExpandAsync();

            // Add to the UI collection
            await RunOnUIThreadAsync(() =>
            {
                Namespaces.Add(rootViewModel);
                SelectedNamespace = rootViewModel; // Select the root namespace
                return Task.CompletedTask;
            });

            PublishSuccessState($"Connected to {effectivePath}");
        }
        catch (Exception ex)
        {
            PublishErrorState($"Error connecting to {computerOrNamespacePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cleanup resources on disposal
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Finds or expands namespaces to reach the target path, starting from the correct root namespace and following the path segments.
    /// </summary>
    private async Task<WmiNamespaceViewModel?> FindOrExpandNamespaceAsync(string targetNamespacePath)
    {
        // Normalize path for comparison
        string Normalize(string path) => path.Trim().TrimEnd('\\').ToLowerInvariant();
        var target = Normalize(targetNamespacePath);

        // First, try to find a root namespace that matches the root of the target path
        var rootMatch = Namespaces.FirstOrDefault(ns => target.StartsWith(Normalize(ns.NamespacePath)));
        if (rootMatch == null)
            return null;

        // If the root itself is the target, return it
        if (Normalize(rootMatch.NamespacePath) == target)
            return rootMatch;

        return await FindOrExpandNamespaceRecursiveAsync(rootMatch, target);
    }

    private async Task<WmiNamespaceViewModel?> FindOrExpandNamespaceRecursiveAsync(WmiNamespaceViewModel current, string target)
    {
        string Normalize(string path) => path.Trim().TrimEnd('\\').ToLowerInvariant();
        if (Normalize(current.NamespacePath) == target)
            return current;

        if (!current.HasLoadedChildren)
            await current.ExpandAsync();

        foreach (var child in current.Children)
        {
            var found = await FindOrExpandNamespaceRecursiveAsync(child, target);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Handles when classes are filtered in the selected namespace to update the status bar
    /// </summary>
    private void HandleClassesFilteredMessage(ClassesFilteredMessage message)
    {
        if (message?.NamespaceViewModel != null && message.NamespaceViewModel == SelectedNamespace)
        {
            UpdateLoadStateStatus();
        }
    }

    /// <summary>
    /// Handles JumpToClassMessage to navigate to the correct namespace and class, handling lazy loading and tab switching.
    /// </summary>
    private async void HandleJumpToClassMessage(JumpToClassMessage message)
    {
        if (message == null)
            return;

        try
        {
            // Debug logging for incoming message
            System.Diagnostics.Debug.WriteLine($"[JumpToClass] Received JumpToClassMessage: NamespacePath='{message.NamespacePath}', ClassName='{message.ClassName}'");

            // Find or expand the namespace path recursively
            var nsVm = await FindOrExpandNamespaceAsync(message.NamespacePath);
            if (nsVm == null)
            {
                PublishErrorState($"Namespace '{message.NamespacePath}' not found.");
                return;
            }

            // Select the namespace
            SelectedNamespace = nsVm;
            nsVm.IsSelected = true;
            nsVm.IsExpanded = true;

            // Ensure classes are loaded
            if (nsVm.ClassLoadState != ClassLoadState.Success)
                await nsVm.LoadClassesAsync();

            // Find the class
            var classVm = nsVm.Classes.FirstOrDefault(c => c.ClassName == message.ClassName);
            if (classVm == null)
            {
                PublishErrorState($"Class '{message.ClassName}' not found in namespace '{message.NamespacePath}'. Check Class Enumeration options.");
                return;
            }

            // Select the class
            nsVm.SelectedClass = classVm;
            classVm.ForceSelection();

            // Publish success state for user feedback
            PublishSuccessState($"Jumped to class '{message.ClassName}' in namespace '{message.NamespacePath}'.");
        }
        catch (Exception ex)
        {
            PublishErrorState($"Jump to class failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Handles when a namespace is selected to ensure it loads its children
    /// </summary>
    private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
    {
        if (message?.NamespaceViewModel == null)
            return;

        // Make sure we always trigger expansion, even for already selected items
        if (!message.NamespaceViewModel.IsExpanded)
        {
            message.NamespaceViewModel.IsExpanded = true;
        }

        // Make sure our local SelectedNamespace property is synchronized
        if (_selectedNamespace != message.NamespaceViewModel)
        {
            _selectedNamespace = message.NamespaceViewModel;
            OnPropertyChanged(nameof(SelectedNamespace));
        }

        // Update the status bar based on the selected namespace
        UpdateLoadStateStatus();
    }

    /// <summary>
    /// Updates the status bar message based on the selected namespace or class load states
    /// </summary>
    private void UpdateLoadStateStatus()
    {
        // If no namespace is selected, do nothing
        if (SelectedNamespace == null || SelectedNamespace.NamespaceLoadState != NamespaceLoadState.Success)
            return;

        // If a class is selected, show status based on class load state
        if (SelectedNamespace.SelectedClass != null)
        {
            var selectedClass = SelectedNamespace.SelectedClass;
            switch (selectedClass.LoadState)
            {
                case InstanceLoadState.Unknown:
                    PublishSuccessState($"Selected class {selectedClass.ClassName}. Double-click to load instances.");
                    break;
                case InstanceLoadState.Loading:
                    PublishBusyState($"Loading instances for class {selectedClass.ClassName}...");
                    break;
                case InstanceLoadState.Warning:
                    PublishWarningState($"Showing partial results for class {selectedClass.ClassName}.");
                    break;
                case InstanceLoadState.Failed:
                    PublishErrorState($"Failed to load instances for class {selectedClass.ClassName}. Double-click class to try again.");
                    break;
                case InstanceLoadState.Success:
                    var count = selectedClass.Instances.Count;
                    PublishSuccessState($"Showing {count} instances for class {selectedClass.ClassName}.");
                    break;
            }
            return;
        }

        // Otherwise, show status based on namespace class load state
        var ns = SelectedNamespace;
        switch (ns.ClassLoadState)
        {
            case ClassLoadState.Unknown:
                PublishSuccessState($"Selected {ns.NamespacePath} Double-click to load classes.");
                break;
            case ClassLoadState.Loading:
                PublishBusyState($"Loading classes for {ns.NamespacePath}...");
                break;
            case ClassLoadState.Warning:
                PublishBusyState($"Showing partial results for {ns.NamespacePath}.");
                break;
            case ClassLoadState.Failed:
                PublishErrorState($"Failed to load classes for {ns.NamespacePath}. Double-click namespace to try again.");
                break;
            case ClassLoadState.Success when ns.NamespaceLoadState == NamespaceLoadState.Success:
                var count = ns.ClassesView.Cast<object>().Count();
                PublishSuccessState($"Showing {count} classes for {ns.NamespacePath}");
                break;
        }
    }
}