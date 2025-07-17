using WmiExplorer.Common.Enums;

namespace WmiExplorer.Common.Models;

/// <summary>
/// Represents the current state of the application with additional metadata
/// </summary>
public class ApplicationState
{
    private ApplicationState(AppState state, string message, Exception? exception = null)
    {
        State = state;
        Message = message;
        Exception = exception;
        Timestamp = DateTime.Now;
    }

    /// <summary>
    /// Optional exception that caused this state
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Message describing the current state
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The current state
    /// </summary>
    public AppState State { get; }

    /// <summary>
    /// When the state was created
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates a Busy state
    /// </summary>
    public static ApplicationState Busy(string message = "Processing...")
        => new ApplicationState(AppState.Busy, message);

    /// <summary>
    /// Creates an Error state
    /// </summary>
    public static ApplicationState Error(string message, Exception? exception = null)
        => new ApplicationState(AppState.Error, message, exception);

    /// <summary>
    /// Creates an Indeterminate state
    /// </summary>
    public static ApplicationState Indeterminate(string message = "Working...")
        => new ApplicationState(AppState.Indeterminate, message);

    /// <summary>
    /// Creates a PartialSuccess state
    /// </summary>
    public static ApplicationState PartialSuccess(string message)
        => new ApplicationState(AppState.PartialSuccess, message);

    /// <summary>
    /// Creates a Ready state
    /// </summary>
    public static ApplicationState Ready(string message = "Ready")
        => new ApplicationState(AppState.Ready, message);

    /// <summary>
    /// Creates a Success state
    /// </summary>
    public static ApplicationState Success(string message)
        => new ApplicationState(AppState.Success, message);

    /// <summary>
    /// Creates a Warning state
    /// </summary>
    public static ApplicationState Warning(string message)
        => new ApplicationState(AppState.Warning, message);
}