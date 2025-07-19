using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Helpers;

/// <summary>
/// Represents the status of a tab, including its current state and message
/// </summary>
public partial class TabStatus : ObservableObject
{
    private readonly IMessengerService? _messengerService;

    [ObservableProperty]
    private AppState _appState = AppState.Ready;

    [ObservableProperty]
    private Exception? _exception;

    [ObservableProperty]
    private string _message = "Ready";

    [ObservableProperty]
    private string? _tooltip;

    /// <summary>
    /// Creates a new TabStatus with the specified state and message
    /// </summary>
    public TabStatus(AppState appState = AppState.Ready, string message = "Ready", string? tooltip = null)
    {
        _appState = appState;
        _message = message;
        _tooltip = tooltip ?? message;
    }

    /// <summary>
    /// Creates a new TabStatus with messenger service for automatic state publishing
    /// </summary>
    public TabStatus(IMessengerService messengerService, AppState appState = AppState.Ready, string message = "Ready", string? tooltip = null)
    {
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
        _appState = appState;
        _message = message;
        _tooltip = tooltip ?? message;
    }

    /// <summary>
    /// Simple tab status with just tooltip
    /// </summary>
    public TabStatus(string tooltip)
    {
        _tooltip = tooltip;
        _appState = AppState.Unknown;
        _message = string.Empty;
        _exception = null;
    }

    /// <summary>
    /// Sets the status to Ready state
    /// </summary>
    public void SetReady(string message = "Ready", string? tooltip = null)
    {
        AppState = AppState.Ready;
        Message = message;
        Exception = null;
        Tooltip = tooltip ?? message;
        PublishStateChange();
    }

    /// <summary>
    /// Sets the status to Busy state
    /// </summary>
    public void SetBusy(string message = "Processing...", string? tooltip = null)
    {
        AppState = AppState.Busy;
        Message = message;
        Exception = null;
        Tooltip = tooltip ?? message;
        PublishStateChange();
    }

    /// <summary>
    /// Sets the status to Success state
    /// </summary>
    public void SetSuccess(string message, string? tooltip = null)
    {
        AppState = AppState.Success;
        Message = message;
        Exception = null;
        Tooltip = tooltip ?? message;
        PublishStateChange();
    }

    /// <summary>
    /// Sets the status to Warning state
    /// </summary>
    public void SetWarning(string message, string? tooltip = null)
    {
        AppState = AppState.Warning;
        Message = message;
        Exception = null;
        Tooltip = tooltip ?? message;
        PublishStateChange();
    }

    /// <summary>
    /// Sets the status to Error state
    /// </summary>
    public void SetError(string message, Exception? exception = null, string? tooltip = null)
    {
        AppState = AppState.Error;
        Message = message;
        Exception = exception;
        Tooltip = tooltip ?? message;
        PublishStateChange();
    }

    /// <summary>
    /// Sets the status to PartialSuccess state
    /// </summary>
    public void SetPartialSuccess(string message, string? tooltip = null)
    {
        AppState = AppState.PartialSuccess;
        Message = message;
        Exception = null;
        Tooltip = tooltip ?? message;
        PublishStateChange();
    }

    /// <summary>
    /// Updates the status based on an ItemStatus object
    /// </summary>
    public void UpdateFromItemStatus(ItemStatus itemStatus)
    {
        var appState = ItemStatus.MapLoadStateToAppState(itemStatus.LoadState);
        AppState = appState;
        Message = itemStatus.StatusMessage;
        Exception = itemStatus.Exception;
        Tooltip = itemStatus.StatusMessage;
    }

    /// <summary>
    /// Creates a TabStatus from an ItemStatus object
    /// </summary>
    public static TabStatus FromItemStatus(ItemStatus itemStatus)
    {
        var tabStatus = new TabStatus();
        tabStatus.UpdateFromItemStatus(itemStatus);
        return tabStatus;
    }

    /// <summary>
    /// Publishes the current state change to the messenger service
    /// </summary>
    private void PublishStateChange()
    {
        if (_messengerService == null)
            return;

        var applicationState = CreateApplicationState();
        _messengerService.Send(new ApplicationStateMessage(applicationState));
    }

    /// <summary>
    /// Creates an ApplicationState based on the current AppState, Message, and Exception
    /// </summary>
    private ApplicationState CreateApplicationState()
    {
        return AppState switch
        {
            AppState.Error => ApplicationState.Error(Message, Exception),
            AppState.Busy => ApplicationState.Busy(Message),
            AppState.Success => ApplicationState.Success(Message),
            AppState.PartialSuccess => ApplicationState.PartialSuccess(Message),
            AppState.Warning => ApplicationState.Warning(Message),
            AppState.Unknown => ApplicationState.Unknown(Message),
            _ => ApplicationState.Ready(Message)
        };
    }
}