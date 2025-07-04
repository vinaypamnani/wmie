using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Namespaces pane. Manages the collection of namespaces
/// and related UI operations for the namespace tree view.
/// </summary>
public partial class NamespacesViewModel : SelectionAwareViewModelBase
{
    private readonly IApplicationService _applicationService;
    private readonly ICacheService _cacheService;
    private readonly ClassesTabViewModel _classesTabViewModel;
    private readonly CancellationTokenSource _cts = new();
    private readonly SettingsManager _settingsManager;
    private readonly WatcherTabViewModel _watcherTabViewModel;
    private readonly IWmiService _wmiService;

    public NamespacesViewModel(
              IMessengerService messengerService,
              SettingsManager settingsManager,
              IWmiService wmiService,
              IApplicationService applicationService,
              ICacheService cacheService,
              ClassesTabViewModel classesTabViewModel,
              WatcherTabViewModel watcherTabViewModel,
              SelectionManager selectionManager) : base(messengerService, selectionManager)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _classesTabViewModel = classesTabViewModel ?? throw new ArgumentNullException(nameof(classesTabViewModel));
        _watcherTabViewModel = watcherTabViewModel ?? throw new ArgumentNullException(nameof(watcherTabViewModel));

        // Subscribe to messages
        StrongSubscribe<JumpToClassMessage>(message => JumpToClassCommand.Execute(message));
        StrongSubscribe<ClassesFilteredMessage>(HandleClassesFilteredMessage);
        StrongSubscribe<DisconnectNamespaceMessage>(HandleDisconnectNamespaceMessage);

        // Subscribe to settings property changes for ShowSystemClasses
        if (_settingsManager is System.ComponentModel.INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += SettingsManager_PropertyChanged;
        }
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
    /// Gets the SettingsManager
    /// </summary>
    public SettingsManager SettingsManager => _settingsManager;

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
                // Just select the existing root namespace using SelectionManager
                // SelectionManager.SetSelectedObject(existingRoot);
                // existingRoot.IsSelected = true;
                // PublishSuccessState($"Already connected. Right-click {effectivePath} to disconnect first before reconnecting.");
                // return;
                DisconnectRoot(existingRoot);

            }

            // Create the root namespace view model using the async method
            var rootViewModel = await WmiNamespaceViewModel.CreateRootAsync(
                effectivePath,
                connectionOptions,
                _wmiService,
                _messengerService,
                _applicationService,
                _settingsManager,
                _cacheService,
                SelectionManager,
                _cts.Token);

            // Load initial children
            await rootViewModel.ExpandAsync();

            // Clear any existing selections
            SelectionManager.ClearSelections();

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
            Log.Error(ex, "Error connecting to WMI namespace: {ComputerOrNamespacePath}", computerOrNamespacePath);
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
    /// Called when the selected namespace changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedNamespaceChanged(WmiNamespaceViewModel? selectedNamespace)
    {
        // Notify command state changes
        ReloadClassesCommand.NotifyCanExecuteChanged();

        // Update the status bar based on the selected namespace
        UpdateStatusBar();
    }

    private void DisconnectRoot(WmiNamespaceViewModel namespaceToRemove)
    {
        // Remove the namespace from the collection
        RunOnUIThread(() =>
        {
            Namespaces.Remove(namespaceToRemove);
        });

        // Dispose the namespace and all its children, classes, and instances
        namespaceToRemove.Dispose();

        PublishSuccessState($"Disconnected from namespace: {namespaceToRemove.NamespacePath}");
        Log.Information("Disconnected from namespace: {NamespacePath}", namespaceToRemove.NamespacePath);
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

        return await FindOrExpandNamespaceByPathAsync(rootMatch, target);
    }

    /// <summary>
    /// Intelligently navigates to the target namespace by following the specific path segments,
    /// avoiding unnecessary expansion of sibling namespaces.
    /// </summary>
    private async Task<WmiNamespaceViewModel?> FindOrExpandNamespaceByPathAsync(WmiNamespaceViewModel current, string target)
    {
        string Normalize(string path) => path.Trim().TrimEnd('\\').ToLowerInvariant();
        var currentPath = Normalize(current.NamespacePath);

        // If we've reached our target, return it
        if (currentPath == target)
            return current;

        // If the target doesn't start with our current path, we're on the wrong branch
        if (!target.StartsWith(currentPath))
            return null;

        // Extract the remaining path after the current namespace
        var remainingPath = target.Substring(currentPath.Length).TrimStart('\\');
        if (string.IsNullOrEmpty(remainingPath))
            return current; // We've reached the target

        // Get the next segment in the path
        var nextSegment = remainingPath.Split('\\')[0];
        var nextTargetPath = currentPath + "\\" + nextSegment;

        // Ensure children are loaded
        if (!current.HasLoadedChildren)
            await current.ExpandAsync();

        // Find the specific child that matches the next segment in our path
        var targetChild = current.Children.FirstOrDefault(child =>
            Normalize(child.NamespacePath) == nextTargetPath);

        if (targetChild == null)
            return null; // Path doesn't exist

        // Recursively continue down the specific path
        return await FindOrExpandNamespaceByPathAsync(targetChild, target);
    }

    /// <summary>
    /// Handles when classes are filtered in the selected namespace to update the status bar
    /// </summary>
    private void HandleClassesFilteredMessage(ClassesFilteredMessage message)
    {
        if (message?.NamespaceViewModel != null && message.NamespaceViewModel == SelectionManager.SelectedNamespace)
        {
            UpdateStatusBar();
        }
    }

    /// <summary>
    /// Handles disconnect namespace message to remove root namespace from tree
    /// </summary>
    private void HandleDisconnectNamespaceMessage(DisconnectNamespaceMessage message)
    {
        if (message?.NamespaceViewModel == null || !message.NamespaceViewModel.IsRoot)
            return;

        DisconnectRoot(message.NamespaceViewModel);
    }

    /// <summary>
    /// Handles JumpToClassMessage to navigate to the correct namespace and class, handling lazy loading and tab switching.
    /// </summary>
    [RelayCommand]
    private async Task JumpToClass(JumpToClassMessage message)
    {
        if (message == null)
            return; try
        {
            // Debug logging for incoming message
            Log.Debug("Received JumpToClassMessage: NamespacePath='{NamespacePath}', ClassName='{ClassName}'",
                message.NamespacePath, message.ClassName);

            // Find or expand the namespace path recursively
            var nsVm = await FindOrExpandNamespaceAsync(message.NamespacePath);
            if (nsVm == null)
            {
                PublishErrorState($"Namespace '{message.NamespacePath}' not found.");
                return;
            }

            // Select the namespace using SelectionManager
            SelectionManager.SetSelectedObject(nsVm);

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

            // Select the class using SelectionManager
            SelectionManager.SetSelectedObject(classVm);

            // Publish success state for user feedback
            PublishSuccessState($"Jumped to class '{message.ClassName}' in namespace '{message.NamespacePath}'.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Jump to class failed: NamespacePath='{NamespacePath}', ClassName='{ClassName}'",
                message.NamespacePath, message.ClassName);
            PublishErrorState($"Jump to class failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Command to reload the classes of the selected namespace
    /// </summary>
    [RelayCommand(CanExecute = nameof(ReloadClassesCanExecute))]
    private void ReloadClasses()
    {
        SelectionManager.SelectedNamespace?.LoadClassesCommand.Execute(null);
    }

    private bool ReloadClassesCanExecute()
    {
        return SelectionManager.SelectedNamespace != null && SelectionManager.SelectedNamespace.LoadClassesCommand.CanExecute(null);
    }

    /// <summary>
    /// Updates the status bar message based on the selected namespace state
    /// </summary>
    private void UpdateStatusBar()
    {
        // If no namespace is selected, do nothing
        if (SelectionManager.SelectedNamespace == null || SelectionManager.SelectedNamespace.NamespaceLoadState != NamespaceLoadState.Success)
            return;

        // Otherwise, show status based on namespace class load state
        var ns = SelectionManager.SelectedNamespace;
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

    private void SettingsManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsManager.ShowSystemClasses))
        {
            // Only update the currently selected namespace
            SelectionManager.SelectedNamespace?.OnShowSystemClassesChanged();
        }
    }
}