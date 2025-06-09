using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Concurrent;
using System.Windows;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels;
using WmiExplorer.Presentation.Views;

namespace WmiExplorer.Services;

/// <summary>
/// Service that wraps IMessenger to provide comprehensive messaging capabilities
/// including strong subscriptions, UI thread dispatching, and message debouncing
/// </summary>
public class MessengerService : IMessengerService, IDisposable
{
    // Debounce interval for all message types (in milliseconds)
    private const int DebounceIntervalMs = 150;

    private readonly IMessenger _messenger;
    private readonly ConcurrentDictionary<object, List<object>> _strongReferences = new();
    private readonly ConcurrentDictionary<object, List<object>> _weakSubscriptions = new();

    // Debouncing infrastructure
    private readonly ConcurrentDictionary<Type, DebounceDispatcher> _debouncers = new();
    private readonly ConcurrentDictionary<Type, object> _latestMessages = new();
    private int _isPublishing;

    public MessengerService(IMessenger messenger)
    {
        _messenger = messenger;
    }

    public void SendImmediate<T>(T message) where T : class
    {
        _messenger.Send(message);
    }

    /// <summary>
    /// Sends a message to all recipients with debouncing to prevent rapid message duplication
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="message">The message to send</param>
    public void Send<T>(T message) where T : class
    {
        if (message == null)
            return;

        var messageType = typeof(T);

        // Store the latest message for this type
        _latestMessages[messageType] = message;

        var debouncer = _debouncers.GetOrAdd(messageType, _ => new DebounceDispatcher(DebounceIntervalMs));

        debouncer.Debounce(() =>
        {
            // Only send if there's still a message of this type waiting
            // This ensures we only send the most recent message
            if (_latestMessages.TryRemove(messageType, out var latestMessageObj) && latestMessageObj is T latestMessage)
            {
                // Special handling for ApplicationStateMessage
                if (message is ApplicationStateMessage appStateMsg && Application.Current != null)
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (MainWindow.Current?.DataContext is MainViewModel mainVm)
                        {
                            mainVm.CurrentApplicationState = appStateMsg.State;
                        }
                    });
                }

                Interlocked.Increment(ref _isPublishing);

                try
                {
                    _messenger.Send(latestMessage);
                }
                finally
                {
                    Interlocked.Decrement(ref _isPublishing);
                }
            }
        });
    }

    public void Subscribe<T>(object subscriber, Action<T> handler, bool runOnUIThread = false) where T : class
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

        // Register with weak reference (using the subscriber as the recipient)
        _messenger.Register<T>(subscriber, (r, m) => wrappedHandler(m));

        // Track this subscription for cleanup
        _weakSubscriptions.AddOrUpdate(subscriber,
            [typeof(T)],
            (key, existing) => { existing.Add(typeof(T)); return existing; });
    }    public void StrongSubscribe<T>(object subscriber, Action<T> handler, bool runOnUIThread = false) where T : class
    {
        var recipient = new StrongReferenceRecipient<T>(handler, runOnUIThread);

        // Keep strong reference to prevent GC
        _strongReferences.AddOrUpdate(subscriber,
            [recipient],
            (key, existing) => { existing.Add(recipient); return existing; });

        // Register the recipient with the messenger
        _messenger.Register<T>(recipient, (r, m) => ((StrongReferenceRecipient<T>)r).HandleMessage(m));
    }    public void Unsubscribe<T>(object subscriber) where T : class
    {
        // Unsubscribe weak subscriptions
        _messenger.Unregister<T>(subscriber);

        // Remove from weak subscription tracking
        if (_weakSubscriptions.TryGetValue(subscriber, out var weakTypes))
        {
            weakTypes.Remove(typeof(T));
            if (weakTypes.Count == 0)
            {
                _weakSubscriptions.TryRemove(subscriber, out _);
            }
        }

        // Unsubscribe strong subscriptions
        if (_strongReferences.TryGetValue(subscriber, out var recipients))
        {
            var toRemove = recipients.OfType<StrongReferenceRecipient<T>>().ToList();
            foreach (var recipient in toRemove)
            {
                _messenger.Unregister<T>(recipient);
                recipients.Remove(recipient);
            }

            if (recipients.Count == 0)
            {
                _strongReferences.TryRemove(subscriber, out _);
            }
        }
    }    public void UnsubscribeAll(object subscriber)
    {
        // Unsubscribe all weak subscriptions
        _messenger.UnregisterAll(subscriber);
        _weakSubscriptions.TryRemove(subscriber, out _);

        // Unsubscribe all strong subscriptions
        if (_strongReferences.TryRemove(subscriber, out var recipients))
        {
            foreach (var recipient in recipients)
            {
                _messenger.UnregisterAll(recipient);
            }
        }
    }

    /// <summary>
    /// Helper method to execute an action on the UI thread
    /// </summary>
    private static void RunOnUIThread(Action action)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            action();
        }
        else
        {
            Application.Current?.Dispatcher?.InvokeAsync(action);
        }
    }

    #region IDisposable

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            foreach (var debouncer in _debouncers.Values)
            {
                debouncer.Dispose();
            }
            _debouncers.Clear();
            _latestMessages.Clear();
        }

        _disposed = true;
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

    public StrongReferenceRecipient(Action<T> handler, bool runOnUIThread)
    {
        _handler = handler;
        _runOnUIThread = runOnUIThread;
    }

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
}
