using System.Collections.Concurrent;
using Application = System.Windows.Application;
using WmiExplorer.Presentation.ViewModels;
using WmiExplorer.Presentation.Views;
using WmiExplorer.Common.Shared;

namespace WmiExplorer.Services;

/// <summary>
/// A robust implementation of the message bus pattern that facilitates communication
/// between components without creating tight coupling.
/// </summary>
public class MessagingService : IMessagingService
{
    private int _isPublishing;

    // Using ConcurrentDictionary for thread safety
    private readonly ConcurrentDictionary<Type, List<SubscriberInfo>> _subscribers = new();

    /// <summary>
    /// Publishes a message to all subscribers
    /// </summary>
    public void Publish<TMessage>(TMessage message)
    {
        if (message == null)
            return;

        // Debug logging for all messages
        System.Diagnostics.Debug.WriteLine($"[MessagingService] Publishing: {typeof(TMessage).Name}");

        // Special handling for ApplicationStateMessage
        if (message is ApplicationStateMessage appStateMsg)
        {
            System.Diagnostics.Debug.WriteLine($"[MessagingService] ApplicationState: {appStateMsg.State.State}, Message={appStateMsg.State.Message}");                // Update the MainWindow status bar
            if (MainWindow.Current != null)
            {
                // Also update the view model for binding
                if (MainWindow.Current.DataContext is MainViewModel mainVm)
                {
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        mainVm.CurrentApplicationState = appStateMsg.State;
                    });
                }
            }
        }

        var messageType = typeof(TMessage);
        if (!_subscribers.TryGetValue(messageType, out var subscribersList) || subscribersList.Count == 0)
        {
            return;
        }

        // Set flag that we're publishing to prevent concurrent modification issues
        Interlocked.Increment(ref _isPublishing);

        try
        {
            // Create a snapshot to avoid issues if the collection changes during enumeration
            var currentSubscribers = subscribersList.ToList();
            var expiredSubscribers = new List<SubscriberInfo>();

            foreach (var subscriber in currentSubscribers)
            {
                if (subscriber.IsAlive && subscriber.IsOwnerAlive())
                {
                    try
                    {
                        // Invoke on UI thread if needed, otherwise invoke directly
                        if (subscriber.ShouldRunOnUIThread && Application.Current != null)
                        {
                            Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                if (subscriber.IsAlive)
                                {
                                    subscriber.DeliverMessage(message);
                                }
                            });
                        }
                        else
                        {
                            subscriber.DeliverMessage(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MessagingService] Error delivering message: {ex.Message}");
                    }
                }
                else
                {
                    expiredSubscribers.Add(subscriber);
                }
            }

            // Clean up expired subscribers
            foreach (var expired in expiredSubscribers)
            {
                RemoveSubscriber(messageType, expired);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _isPublishing);
        }
    }

    /// <summary>
    /// Subscribes to a specific message type using strong references to prevent garbage collection
    /// </summary>
    public IDisposable StrongSubscribe<TMessage>(Action<TMessage> action, bool runOnUIThread = false)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        var messageType = typeof(TMessage);
        var subscriber = new StrongSubscriberInfo<TMessage>(action, runOnUIThread);

        return AddSubscriber(messageType, subscriber, "StrongSubscription");
    }

    /// <summary>
    /// Subscribes to a specific message type with thread-safe handling
    /// </summary>
    public IDisposable Subscribe<TMessage>(Action<TMessage> action, bool runOnUIThread = false)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        var messageType = typeof(TMessage);
        var owner = new WeakReference<object>(action.Target ?? this);
        var subscriber = new SubscriberInfo<TMessage>(action, owner, runOnUIThread);

        return AddSubscriber(messageType, subscriber, "Subscription");
    }

    /// <summary>
    /// Common method to add subscribers with thread-safe handling
    /// </summary>
    private IDisposable AddSubscriber(Type messageType, SubscriberInfo subscriber, string subscriptionType)
    {
        // Get or add thread-safely
        var subscribersList = _subscribers.GetOrAdd(
            messageType,
            _ => new List<SubscriberInfo>()
        );

        // Wait if publishing is in progress
        while (Interlocked.CompareExchange(ref _isPublishing, 0, 0) != 0)
        {
            Thread.Sleep(1);
        }

        // Add the subscriber
        lock (subscribersList)
        {
            subscribersList.Add(subscriber);
        }

        System.Diagnostics.Debug.WriteLine($"[MessagingService] {subscriptionType} to {messageType.Name}, RunOnUIThread={subscriber.ShouldRunOnUIThread}");

        return new SubscriptionToken(
            () => RemoveSubscriber(messageType, subscriber),
            $"{subscriptionType}: {messageType.Name}"
        );
    }

    /// <summary>
    /// Thread-safe removal of a subscriber
    /// </summary>
    private void RemoveSubscriber(Type messageType, SubscriberInfo subscriber)
    {
        // If we're currently publishing, defer the remove operation
        if (Interlocked.CompareExchange(ref _isPublishing, 0, 0) != 0)
        {
            Application.Current?.Dispatcher.InvokeAsync(() => RemoveSubscriber(messageType, subscriber));
            return;
        }

        if (_subscribers.TryGetValue(messageType, out var subscribers))
        {
            lock (subscribers)
            {
                subscribers.Remove(subscriber);

                if (subscribers.Count == 0)
                {
                    _subscribers.TryRemove(messageType, out _);
                }
            }
        }
    }

    /// <summary>
    /// Type-specific subscriber that maintains strong references to prevent garbage collection
    /// </summary>
    private class StrongSubscriberInfo<TMessage> : SubscriberInfo
    {
        private readonly Action<TMessage> _action;

        // Strong reference

        public override bool IsAlive => true;

        // Always alive since we maintain strong reference

        public override void DeliverMessage<T>(T message)
        {
            if (message is TMessage typedMessage)
            {
                _action(typedMessage);
            }
        }

        public StrongSubscriberInfo(Action<TMessage> action, bool runOnUIThread)
            : base(new WeakReference<object>(action.Target ?? action), runOnUIThread)
        {
            _action = action; // Keep strong reference
        }
    }

    /// <summary>
    /// Abstract base class for subscriber information
    /// </summary>
    private abstract class SubscriberInfo
    {
        protected SubscriberInfo(WeakReference<object> owner, bool runOnUIThread)
        {
            Owner = owner;
            ShouldRunOnUIThread = runOnUIThread;
        }

        public abstract bool IsAlive { get; }
        public WeakReference<object> Owner { get; }
        public bool ShouldRunOnUIThread { get; }

        public abstract void DeliverMessage<TMessage>(TMessage message);

        public bool IsOwnerAlive()
        {
            return Owner.TryGetTarget(out _);
        }
    }

    /// <summary>
    /// Type-specific subscriber that can deliver properly typed messages using weak references
    /// </summary>
    private class SubscriberInfo<TMessage> : SubscriberInfo
    {
        private readonly WeakReference<Action<TMessage>> _action;

        public override bool IsAlive => _action.TryGetTarget(out _);

        public override void DeliverMessage<T>(T message)
        {
            if (message is TMessage typedMessage && _action.TryGetTarget(out var action))
            {
                action(typedMessage);
            }
        }

        public SubscriberInfo(Action<TMessage> action, WeakReference<object> owner, bool runOnUIThread)
            : base(owner, runOnUIThread)
        {
            _action = new WeakReference<Action<TMessage>>(action);
        }
    }

    /// <summary>
    /// Token returned when subscribing that can be used to unsubscribe
    /// </summary>
    private class SubscriptionToken : IDisposable
    {
        private readonly string _description;
        private readonly Action _unsubscribeAction;

        public SubscriptionToken(Action unsubscribeAction, string description)
        {
            _unsubscribeAction = unsubscribeAction;
            _description = description;
        }

        #region IDisposable
        private bool _isDisposed;

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _unsubscribeAction?.Invoke();
                _isDisposed = true;
                System.Diagnostics.Debug.WriteLine($"[MessagingService] Disposed {_description}");
            }
        }

        #endregion
    }
}