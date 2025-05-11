using System.Windows;
using WmiExplorer.Common.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Common.Base
{
    /// <summary>
    /// Base class for ViewModels that need messaging capabilities
    /// Extends ViewModelBase with messaging functionality
    /// </summary>
    public abstract class MessagingViewModelBase : ViewModelBase, IDisposable
    {
        private readonly List<IDisposable> _messageSubscriptions = new();
        private readonly Dictionary<object, Delegate> _strongHandlers = new Dictionary<object, Delegate>();
        private bool _isDisposed;

        /// <summary>
        /// Gets the messaging service for publishing and subscribing to messages
        /// </summary>
        protected IMessagingService? MessageService { get; private set; }

        /// <summary>
        /// Releases resources
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Clean up all message subscriptions
                    foreach (var subscription in _messageSubscriptions)
                    {
                        subscription?.Dispose();
                    }
                    _messageSubscriptions.Clear();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Initializes messaging support for this view model
        /// </summary>
        /// <param name="messagingService">The messaging service to use</param>
        protected void InitializeMessaging(IMessagingService messagingService)
        {
            MessageService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
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
            MessageService?.Publish(message);
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
        /// Subscribe to a message of type T and keep a strong reference to the handler
        /// </summary>
        /// <typeparam name="T">The message type</typeparam>
        /// <param name="handler">The handler to call when a message is received</param>
        /// <param name="runOnUIThread">Whether the handler should be run on the UI thread</param>
        /// <returns>A subscription token that can be used to unsubscribe</returns>
        protected IDisposable StrongSubscribe<T>(Action<T> handler, bool runOnUIThread = false) where T : class
        {
            if (MessageService == null)
                throw new InvalidOperationException("Call InitializeMessaging before subscribing to messages");

            // Store the handler with a unique key to prevent garbage collection
            _strongHandlers[Guid.NewGuid()] = handler;

            var subscription = MessageService.Subscribe(handler, runOnUIThread);
            _messageSubscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// Subscribe to a message of type T
        /// </summary>
        /// <typeparam name="T">The message type</typeparam>
        /// <param name="handler">The handler to call when a message is received</param>
        /// <param name="runOnUIThread">Whether the handler should be run on the UI thread</param>
        /// <returns>A subscription token that can be used to unsubscribe</returns>
        protected IDisposable Subscribe<T>(Action<T> handler, bool runOnUIThread = false) where T : class
        {
            if (MessageService == null)
                throw new InvalidOperationException("Call InitializeMessaging before subscribing to messages");

            var subscription = MessageService.Subscribe(handler, runOnUIThread);
            _messageSubscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}