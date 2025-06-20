using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Common.Base;

/// <summary>
/// Base class for ViewModels that need both messaging capabilities and SelectionManager access.
/// Provides centralized SelectionManager handling with virtual methods for property change events.
/// </summary>
public abstract partial class SelectionAwareViewModelBase : MessagingViewModelBase
{
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

        // Subscribe to SelectionManager property changes
        SelectionManager.PropertyChanged += OnSelectionManagerPropertyChanged;
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
    /// Handles SelectionManager property changes. Routes to specific virtual methods based on the property name.
    /// Override the specific On*Changed methods in derived classes instead of this method.
    /// </summary>
    private void OnSelectionManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectionManager.SelectedNamespace):
                OnSelectedNamespaceChanged(SelectionManager.SelectedNamespace);
                break;
            case nameof(SelectionManager.SelectedClass):
                OnSelectedClassChanged(SelectionManager.SelectedClass);
                break;
            case nameof(SelectionManager.SelectedInstance):
                OnSelectedInstanceChanged(SelectionManager.SelectedInstance);
                break;
        }
    }
}