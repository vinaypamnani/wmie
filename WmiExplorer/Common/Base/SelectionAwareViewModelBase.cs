using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Common.Base;

/// <summary>
/// Base class for ViewModels that need both messaging capabilities and SelectionManager access.
/// Provides centralized SelectionManager handling with virtual methods for property change events.
/// </summary>
public abstract partial class SelectionAwareViewModelBase : MessagingViewModelBase
{
    private object? _lastProcessedClass;
    private object? _lastProcessedInstance;

    // Track the last processed selections to prevent duplicate processing
    private object? _lastProcessedNamespace;

    /// <summary>
    /// The centralized SelectionManager exposed as ObservableProperty for XAML binding
    /// </summary>
    [ObservableProperty]
    private SelectionManager _selectionManager = null!;

    /// <summary>
    /// Initializes a new instance of the SelectionAwareViewModelBase class
    /// </summary>
    /// <param name="messengerService">The messenger service to use</param>
    /// <param name="selectionManager">The selection manager to use</param>
    protected SelectionAwareViewModelBase(
        IMessengerService messengerService,
        SelectionManager selectionManager) : base(messengerService)
    {
        SelectionManager = selectionManager ?? throw new ArgumentNullException(nameof(selectionManager));

        // Subscribe to SelectionManager property changes for value changes
        SelectionManager.PropertyChanged += OnSelectionManagerPropertyChanged;

        // Subscribe to SelectionChangedMessage for force refresh scenarios (re-selection of same item)
        StrongSubscribe<SelectionChangedMessage>(OnSelectionChangedMessage);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from SelectionManager property changes
            SelectionManager.PropertyChanged -= OnSelectionManagerPropertyChanged;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Called when the selected class changes. Override in derived classes to handle class selection changes.
    /// </summary>
    /// <param name="selectedClass">The newly selected class</param>
    protected virtual void OnSelectedClassChanged(WmiExplorer.Presentation.ViewModels.Items.WmiClassViewModel? selectedClass)
    {
        // Default implementation does nothing - override in derived classes as needed
    }

    /// <summary>
    /// Called when the selected instance changes. Override in derived classes to handle instance selection changes.
    /// </summary>
    /// <param name="selectedInstance">The newly selected instance</param>
    protected virtual void OnSelectedInstanceChanged(WmiExplorer.Presentation.ViewModels.Items.WmiInstanceViewModel? selectedInstance)
    {
        // Default implementation does nothing - override in derived classes as needed
    }

    /// <summary>
    /// Called when the selected namespace changes. Override in derived classes to handle namespace selection changes.
    /// </summary>
    /// <param name="selectedNamespace">The newly selected namespace</param>
    protected virtual void OnSelectedNamespaceChanged(WmiExplorer.Presentation.ViewModels.Items.WmiNamespaceViewModel? selectedNamespace)
    {
        // Default implementation does nothing - override in derived classes as needed
    }

    /// <summary>
    /// Handles SelectionChangedMessage for both new selections and re-selections.
    /// This ensures that selection handlers are called reliably, serving as a backup
    /// to PropertyChanged events and handling force refresh scenarios.
    /// Uses tracking to prevent duplicate processing.
    /// </summary>
    private void OnSelectionChangedMessage(SelectionChangedMessage message)
    {
        if (message?.SelectionManager == null) return;

        var selectedObject = message.SelectionManager.SelectedObject;

        switch (selectedObject)
        {
            case WmiExplorer.Presentation.ViewModels.Items.WmiNamespaceViewModel namespaceVm:
                // Only process if this is different from last processed or if it's a re-selection
                var currentNamespace = SelectionManager.SelectedNamespace;
                if (currentNamespace == namespaceVm && !ReferenceEquals(_lastProcessedNamespace, namespaceVm))
                {
                    _lastProcessedNamespace = namespaceVm;
                    OnSelectedNamespaceChanged(namespaceVm);
                }
                break;
            case WmiExplorer.Presentation.ViewModels.Items.WmiClassViewModel classVm:
                // Only process if this is different from last processed or if it's a re-selection
                var currentClass = SelectionManager.GetSelectedClass();
                if (currentClass == classVm && !ReferenceEquals(_lastProcessedClass, classVm))
                {
                    _lastProcessedClass = classVm;
                    OnSelectedClassChanged(classVm);
                }
                break;
            case WmiExplorer.Presentation.ViewModels.Items.WmiInstanceViewModel instanceVm:
                // Only process if this is different from last processed or if it's a re-selection
                var currentInstance = SelectionManager.GetSelectedInstance();
                if (currentInstance == instanceVm && !ReferenceEquals(_lastProcessedInstance, instanceVm))
                {
                    _lastProcessedInstance = instanceVm;
                    OnSelectedInstanceChanged(instanceVm);
                }
                break;
        }
    }

    /// <summary>
    /// Handles SelectionManager property changes. Routes to specific virtual methods based on the property name.
    /// Uses tracking to prevent duplicate processing.
    /// Override the specific On*Changed methods in derived classes instead of this method.
    /// </summary>
    private void OnSelectionManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectionManager.SelectedNamespace):
                var selectedNamespace = SelectionManager.SelectedNamespace;
                if (!ReferenceEquals(_lastProcessedNamespace, selectedNamespace))
                {
                    _lastProcessedNamespace = selectedNamespace;
                    OnSelectedNamespaceChanged(selectedNamespace);
                }
                break;
            case nameof(SelectionManager.SelectedClass):
                var selectedClass = SelectionManager.GetSelectedClass();
                if (!ReferenceEquals(_lastProcessedClass, selectedClass))
                {
                    _lastProcessedClass = selectedClass;
                    OnSelectedClassChanged(selectedClass);
                }
                break;
            case nameof(SelectionManager.SelectedInstance):
                var selectedInstance = SelectionManager.GetSelectedInstance();
                if (!ReferenceEquals(_lastProcessedInstance, selectedInstance))
                {
                    _lastProcessedInstance = selectedInstance;
                    OnSelectedInstanceChanged(selectedInstance);
                }
                break;
        }
    }
}