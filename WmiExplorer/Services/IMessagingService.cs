namespace WmiExplorer.Services;

public interface IMessagingService
{
    /// <summary>
    /// Publishes a message to all subscribers
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish</typeparam>
    /// <param name="message">The message to publish</param>
    void Publish<TMessage>(TMessage message);

    /// <summary>
    /// Subscribes to a specific message type using weak references
    /// </summary>
    /// <typeparam name="TMessage">The type of message to subscribe to</typeparam>
    /// <param name="action">The action to execute when message is received</param>
    /// <param name="runOnUIThread">Whether the action should be executed on the UI thread</param>
    /// <returns>A subscription token that can be used to unsubscribe</returns>
    IDisposable Subscribe<TMessage>(Action<TMessage> action, bool runOnUIThread = false);

    /// <summary>
    /// Subscribes to a specific message type using strong references to prevent garbage collection
    /// </summary>
    /// <typeparam name="TMessage">The type of message to subscribe to</typeparam>
    /// <param name="action">The action to execute when message is received</param>
    /// <param name="runOnUIThread">Whether the action should be executed on the UI thread</param>
    /// <returns>A subscription token that can be used to unsubscribe</returns>
    IDisposable StrongSubscribe<TMessage>(Action<TMessage> action, bool runOnUIThread = false);
}