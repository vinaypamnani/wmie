namespace WmiExplorer.Services;

/// <summary>
/// Service that wraps IMessenger to provide comprehensive messaging capabilities
/// including strong subscriptions, UI thread dispatching, and message debouncing
/// </summary>
public interface IMessengerService : IDisposable
{
    /// <summary>
    /// Sends a message to all recipients with debouncing to prevent rapid message duplication
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="message">The message to send</param>
    void Send<T>(T message) where T : class;

    /// <summary>
    /// Sends a message to all registered recipients
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="message">The message to send</param>
    void SendImmediate<T>(T message) where T : class;

    /// <summary>
    /// Subscribe to a message of type T with a strong reference to prevent garbage collection
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="subscriber">The subscriber object used as a key for unsubscription</param>
    /// <param name="handler">The handler to call when a message is received</param>
    /// <param name="runOnUIThread">Whether the handler should be run on the UI thread</param>
    void StrongSubscribe<T>(object subscriber, Action<T> handler, bool runOnUIThread = false) where T : class;

    /// <summary>
    /// Subscribe to a message of type T with a weak reference
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="subscriber">The subscriber object used as a key for unsubscription</param>
    /// <param name="handler">The handler to call when a message is received</param>
    /// <param name="runOnUIThread">Whether the handler should be run on the UI thread</param>
    void Subscribe<T>(object subscriber, Action<T> handler, bool runOnUIThread = false) where T : class;

    /// <summary>
    /// Unsubscribe from a specific message type
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="subscriber">The subscriber object to unsubscribe</param>
    void Unsubscribe<T>(object subscriber) where T : class;

    /// <summary>
    /// Unsubscribe from all message types for a given subscriber
    /// </summary>
    /// <param name="subscriber">The subscriber object to unsubscribe from all messages</param>
    void UnsubscribeAll(object subscriber);
}