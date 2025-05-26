using System.Diagnostics;
using WmiExplorer.Common.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Common.Helpers;

/// <summary>
/// Helper class for tracking elapsed time of long-running operations
/// </summary>
public class OperationTimer : IDisposable
{
    private readonly IMessagingService _messagingService;
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    private readonly System.Timers.Timer _updateTimer;
    private readonly int? _clearAfterSeconds;

    /// <summary>
    /// Creates and starts a new operation timer
    /// </summary>
    /// <param name="operationName">Name of the operation being timed</param>
    /// <param name="messagingService">Messaging service to publish elapsed time updates</param>
    /// <param name="updateIntervalMs">How often to publish elapsed time updates (default 1000ms)</param>
    /// <param name="clearAfterSeconds">Optional: seconds after which to clear the elapsed time message</param>
    public OperationTimer(string operationName, IMessagingService messagingService, int updateIntervalMs = 1000, int? clearAfterSeconds = null)
    {
        _operationName = operationName;
        _messagingService = messagingService;
        _stopwatch = Stopwatch.StartNew();
        _clearAfterSeconds = clearAfterSeconds;

        // Create timer to periodically update elapsed time
        _updateTimer = new System.Timers.Timer(updateIntervalMs);
        _updateTimer.Elapsed += (s, e) => PublishElapsedTime();
        _updateTimer.Start();

        // Publish initial message
        PublishElapsedTime();
    }

    /// <summary>
    /// Gets the elapsed time since the timer was started
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Creates and starts a new operation timer
    /// </summary>
    /// <param name="operationName">Name of the operation being timed</param>
    /// <param name="messagingService">Messaging service to publish elapsed time updates</param>
    /// <param name="updateIntervalMs">How often to publish elapsed time updates (default 1000ms)</param>
    /// <param name="clearAfterSeconds">Optional: seconds after which to clear the elapsed time message</param>
    /// <returns>A new OperationTimer instance</returns>
    public static OperationTimer Start(string operationName, IMessagingService messagingService, int updateIntervalMs = 1000, int? clearAfterSeconds = null)
    {
        return new OperationTimer(operationName, messagingService, updateIntervalMs, clearAfterSeconds);
    }

    /// <summary>
    /// Stops the timer and publishes a final elapsed time message
    /// </summary>
    /// <param name="clearAfterSeconds">Optional: seconds after which to clear the elapsed time message (overrides constructor value if specified)</param>
    public void Stop(int? clearAfterSeconds = null)
    {
        if (_disposed) return;

        _stopwatch.Stop();
        _updateTimer.Stop();

        // Publish final elapsed time
        PublishElapsedTime();

        int? clearDelay = clearAfterSeconds ?? _clearAfterSeconds;
        if (clearDelay.HasValue)
        {
            // Clear the elapsed time message after the specified delay
            Task.Delay(clearDelay.Value * 1000).ContinueWith(_ =>
            {
                if (!_disposed)
                {
                    _messagingService.Publish(ElapsedTimeMessage.Clear());
                }
            });
        }
    }

    /// <summary>
    /// Publishes the current elapsed time
    /// </summary>
    private void PublishElapsedTime()
    {
        if (_disposed) return;

        var message = ElapsedTimeMessage.Create(_operationName, _stopwatch.Elapsed);
        _messagingService.Publish(message);
    }

    #region IDisposable
    private bool _disposed = false;

    /// <summary>
    /// Disposes the timer and clears the elapsed time message
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _stopwatch.Stop();
        _updateTimer.Stop();
        _updateTimer.Dispose();

        // Clear the elapsed time message
        // _messagingService.Publish(ElapsedTimeMessage.Clear());

        GC.SuppressFinalize(this);
    }

    #endregion
}