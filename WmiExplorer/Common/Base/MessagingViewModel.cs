using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using WmiExplorer.Common.Shared;

namespace WmiExplorer.Common.Base;

/// <summary>
/// Base class for ViewModels that need messaging capabilities
/// Uses CommunityToolkit.Mvvm.Messaging for messaging functionality
/// </summary>
public abstract partial class MessagingViewModel : DisposableObservableObject
{
    /// <summary>
    /// Initializes a new instance of the MessagingViewModel class
    /// </summary>
    /// <param name="messenger">The messenger to use (defaults to WeakReferenceMessenger.Default)</param>
    protected MessagingViewModel(IMessenger? messenger = null)
    {
        Messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    /// <summary>
    /// Gets the messenger instance used for messaging
    /// </summary>
    protected IMessenger Messenger { get; }

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
        Messenger.Send(message);
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
        // Create a recipient object that will hold a strong reference to the handler
        var recipient = new StrongReferenceRecipient<T>(handler, runOnUIThread);

        // Store the recipient so it doesn't get garbage collected
        TrackDisposable(new DisposableAction(() => Messenger.Unregister<T>(recipient)));

        // Register the recipient
        Messenger.Register<T>(recipient, (r, m) => ((StrongReferenceRecipient<T>)r).HandleMessage(m));
    }

    /// <summary>
    /// Subscribe to a message of type T with a weak reference
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="handler">The handler to call when a message is received</param>
    /// <param name="runOnUIThread">Whether the handler should be run on the UI thread</param>
    protected void Subscribe<T>(Action<T> handler, bool runOnUIThread = false) where T : class
    {
        // Create a wrapper that handles UI thread dispatch if needed
        Action<T> wrappedHandler = message =>
        {
            if (runOnUIThread)
            {
                RunOnUIThread(() => handler(message));
            }
            else
            {
                handler(message);
            }
        };

        // Register with the messenger
        Messenger.Register<T>(this, (r, m) => wrappedHandler(m));
    }

    /// <summary>
    /// Unsubscribe from a message type
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    protected void Unsubscribe<T>() where T : class
    {
        Messenger.Unregister<T>(this);
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

/// <summary>
/// Helper class that maintains strong references to message handlers
/// </summary>
internal class StrongReferenceRecipient<T> where T : class
{
    private readonly Action<T> _handler;
    private readonly bool _runOnUIThread;

    public void HandleMessage(T message)
    {
        if (_runOnUIThread && Application.Current != null)
        {
            Application.Current.Dispatcher.InvokeAsync(() => _handler(message));
        }
        else
        {
            _handler(message);
        }
    }

    public StrongReferenceRecipient(Action<T> handler, bool runOnUIThread)
    {
        _handler = handler;
        _runOnUIThread = runOnUIThread;
    }
}