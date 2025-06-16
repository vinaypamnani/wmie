using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Namespaces pane. Manages the collection of namespaces
/// and related UI operations for the namespace tree view.
/// </summary>
public partial class NamespacesViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;
    private readonly ICacheService _cacheService;
    private readonly ClassesTabViewModel _classesTabViewModel;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private WmiNamespaceViewModel? _selectedNamespace;

    private readonly ISelectionService _selectionService;
    private readonly ISettingsService _settingsService;
    private readonly WatcherTabViewModel _watcherTabViewModel;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    private readonly IWmiService _wmiService;

    public NamespacesViewModel(
              IMessengerService messengerService,
              ISettingsService settingsService,
              IWmiService wmiService,
              IApplicationService applicationService,
              ICacheService cacheService,
              ClassesTabViewModel classesTabViewModel,
              WatcherTabViewModel watcherTabViewModel,
              ISelectionService selectionService) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _classesTabViewModel = classesTabViewModel ?? throw new ArgumentNullException(nameof(classesTabViewModel));
        _watcherTabViewModel = watcherTabViewModel ?? throw new ArgumentNullException(nameof(watcherTabViewModel)); _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));

        // Subscribe to messages
        StrongSubscribe<JumpToClassMessage>(message => JumpToClassCommand.Execute(message));
        StrongSubscribe<ClassesFilteredMessage>(HandleClassesFilteredMessage);
        StrongSubscribe<SelectionChangedMessage>(HandleSelectionChangedMessage);

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;
    }

    /// <summary>
    /// Gets the ClassesTabViewModel
    /// </summary>
    public ClassesTabViewModel ClassesTabViewModel => _classesTabViewModel;

    /// <summary>
    /// Collection of WMI namespaces in the tree
    /// </summary>
    public ObservableCollection<WmiNamespaceViewModel> Namespaces { get; } = new();

    /// <summary>
    /// Gets the view model for the WMI Event Watcher
    /// </summary>
    public WatcherTabViewModel WatcherTabViewModel => _watcherTabViewModel;

    /// <summary>
    /// Connects to the specified computer or namespace path with specified connection options
    /// </summary>
    /// <param name="computerOrNamespacePath">Computer name or namespace path</param>
    /// <param name="connectionOptions">Connection options to use for the connection</param>
    public async Task ConnectAsync(string computerOrNamespacePath, ConnectionOptions connectionOptions)
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
                connectionOptions,
                _wmiService,
                _messengerService,
                _applicationService,
                _settingsService,
                _cacheService,
                _selectionService,
                _cts.Token);

            // Load initial children
            await rootViewModel.ExpandAsync();

            // Add to the UI collection
            await RunOnUIThreadAsync(() =>
            {
                Namespaces.Add(rootViewModel);
                rootViewModel.IsSelected = true;
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
            UpdateStatusBar();
        }
    }

    /// <summary>
    /// Handles the unified selection changed message to update SelectedNamespace
    /// </summary>
    private void HandleSelectionChangedMessage(SelectionChangedMessage message)
    {
        if (message?.SelectionService == null)
            return;

        var selectedObject = message.SelectionService.SelectedObject;

        // Only respond to namespace selections
        if (selectedObject is WmiNamespaceViewModel namespaceVm && namespaceVm != SelectedNamespace)
        {
            SelectedNamespace = namespaceVm;
        }
    }

    /// <summary>
    /// Handles JumpToClassMessage to navigate to the correct namespace and class, handling lazy loading and tab switching.
    /// </summary>
    [RelayCommand]
    private async Task JumpToClass(JumpToClassMessage message)
    {
        if (message == null)
            return;

        try
        {
            // Debug logging for incoming message
            System.Diagnostics.Debug.WriteLine($"[NamespacesViewModel] Received JumpToClassMessage: NamespacePath='{message.NamespacePath}', ClassName='{message.ClassName}'");

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
            classVm.IsSelected = true;

            // Publish success state for user feedback
            PublishSuccessState($"Jumped to class '{message.ClassName}' in namespace '{message.NamespacePath}'.");
        }
        catch (Exception ex)
        {
            PublishErrorState($"Jump to class failed: {ex.Message}", ex);
        }
    }

    partial void OnSelectedNamespaceChanged(WmiNamespaceViewModel? value)
    {
        // Update the status bar based on the selected namespace - this is updated by binding via TreeViewSelectedItemBehavior
        UpdateStatusBar();
    }

    /// <summary>
    /// Command to reload the classes of the selected namespace
    /// </summary>
    [RelayCommand(CanExecute = nameof(ReloadClassesCanExecute))]
    private void ReloadClasses()
    {
        SelectedNamespace?.LoadClassesCommand.Execute(null);
    }

    private bool ReloadClassesCanExecute()
    {
        return SelectedNamespace != null && SelectedNamespace.LoadClassesCommand.CanExecute(null);
    }

    /// <summary>
    /// Updates the status bar message based on the selected namespace state
    /// </summary>
    private void UpdateStatusBar()
    {
        // If no namespace is selected, do nothing
        if (SelectedNamespace == null || SelectedNamespace.NamespaceLoadState != NamespaceLoadState.Success)
            return;

        // Otherwise, show status based on namespace class load state
        var ns = SelectedNamespace;
        switch (ns.ClassLoadState)
        {
            case ClassLoadState.Unknown:
                PublishSuccessState($"Selected {ns.NamespacePath} Double-click the namespace to load classes.");
                break;
            case ClassLoadState.Loading:
                PublishBusyState($"Loading classes for {ns.NamespacePath}...");
                break;
            case ClassLoadState.Warning:
                PublishBusyState($"Showing partial results for {ns.NamespacePath}.");
                break;
            case ClassLoadState.Failed:
                PublishErrorState($"Failed to load classes for {ns.NamespacePath}. Double-click the namespace to try again.");
                break;
            case ClassLoadState.Success when ns.NamespaceLoadState == NamespaceLoadState.Success:
                var count = ns.ClassesView.Cast<object>().Count();
                var total = ns.Classes.Count;
                if (count < total)
                    PublishSuccessState($"Showing {count} of {total} classes for {ns.NamespacePath}.");
                else
                    PublishSuccessState($"Showing {count} classes for {ns.NamespacePath}");
                break;
        }
    }
}