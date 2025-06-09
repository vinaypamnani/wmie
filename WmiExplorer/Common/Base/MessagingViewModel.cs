using System.Windows;
using WmiExplorer.Common.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Common.Base;

/// <summary>
/// Base class for ViewModels that need messaging capabilities
/// Uses IMessengerService for comprehensive messaging functionality
/// </summary>
public abstract partial class MessagingViewModel : DisposableObservableObject
{
    protected readonly IMessengerService _messengerService;

    /// <summary>
    /// Initializes a new instance of the MessagingViewModel class
    /// </summary>
    /// <param name="messengerService">The messenger service to use</param>
    protected MessagingViewModel(IMessengerService messengerService)
    {
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up all message subscriptions
            _messengerService.UnsubscribeAll(this);
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Helper method for publishing busy application state
    /// </summary>
    /// <param name="message">The message to display</param>
    protected void PublishBusyState(string message)
    {
        PublishMessage(new ApplicationStateMessage(ApplicationState.Busy(message)));
    }

    /// <summary>
    /// Helper method for publishing error application state
    /// </summary>
    /// <param name="message">The error message to display</param>
    /// <param name="exception">Optional exception that caused the error</param>
    protected void PublishErrorState(string message, Exception? exception = null)
    {
        PublishMessage(new ApplicationStateMessage(ApplicationState.Error(message, exception)));
    }

    /// <summary>
    /// Publish a message
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="message">The message to publish</param>
    protected void PublishMessage<T>(T message) where T : class
    {
        _messengerService.Send(message);
    }

    /// <summary>
    /// Helper method for publishing ready application state
    /// </summary>
    /// <param name="message">The message to display</param>
    protected void PublishReadyState(string message = "Ready")
    {
        PublishMessage(new ApplicationStateMessage(ApplicationState.Ready(message)));
    }

    /// <summary>
    /// Helper method for publishing success application state
    /// </summary>
    /// <param name="message">The message to display</param>
    protected void PublishSuccessState(string message)
    {
        PublishMessage(new ApplicationStateMessage(ApplicationState.Success(message)));
    }

    /// <summary>
    /// Helper method for publishing warning application state
    /// </summary>
    /// <param name="message">The warning message to display</param>
    protected void PublishWarningState(string message)
    {
        PublishMessage(new ApplicationStateMessage(ApplicationState.Warning(message)));
    }

    /// <summary>
    /// Helper method to execute an action on the UI thread
    /// </summary>
    /// <param name="action">The action to execute</param>
    protected void RunOnUIThread(Action action)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Application.Current.Dispatcher.InvokeAsync(action);
        }
    }

    /// <summary>
    /// Helper method to execute an async action on the UI thread
    /// </summary>
    /// <param name="asyncAction">The async action to execute</param>
    /// <returns>A task representing the async operation</returns>
    protected Task RunOnUIThreadAsync(Func<Task> asyncAction)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            return asyncAction();
        }
        else
        {
            return Application.Current.Dispatcher.InvokeAsync(asyncAction).Task;
        }
    }

    /// <summary>
    /// Subscribe to a message of type T with a strong reference to the handler
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="handler">The handler to call when a message is received</param>
    /// <param name="runOnUIThread">Whether the handler should be run on the UI thread</param>
    protected void StrongSubscribe<T>(Action<T> handler, bool runOnUIThread = false) where T : class
    {
        _messengerService.StrongSubscribe<T>(this, handler, runOnUIThread);
        TrackDisposable(new DisposableAction(() => _messengerService.Unsubscribe<T>(this)));
    }

    /// <summary>
    /// Subscribe to a message of type T with a weak reference
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="handler">The handler to call when a message is received</param>
    /// <param name="runOnUIThread">Whether the handler should be run on the UI thread</param>
    protected void Subscribe<T>(Action<T> handler, bool runOnUIThread = false) where T : class
    {
        _messengerService.Subscribe<T>(this, handler, runOnUIThread);
        TrackDisposable(new DisposableAction(() => _messengerService.Unsubscribe<T>(this)));
    }

    /// <summary>
    /// Unsubscribe from a message type
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    protected void Unsubscribe<T>() where T : class
    {
        _messengerService.Unsubscribe<T>(this);
    }
}

/// <summary>
/// A simple implementation of IDisposable that executes an action when disposed
/// </summary>
internal class DisposableAction : IDisposable
{
    private readonly Action _action;

    public DisposableAction(Action action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    #region IDisposable
    private bool _isDisposed;

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _action();
            _isDisposed = true;
        }
    }

    #endregion
}