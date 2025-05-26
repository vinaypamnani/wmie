namespace WmiExplorer.Common.Shared;

/// <summary>
/// Message containing elapsed time information for long-running operations
/// </summary>
public class ElapsedTimeMessage : MessageBase
{
    public ElapsedTimeMessage(string message)
    {
        Message = message;
        Timestamp = DateTime.Now;
    }

    /// <summary>
    /// The elapsed time message to display
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// When this message was created
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates an empty elapsed time message to clear the display
    /// </summary>
    public static ElapsedTimeMessage Clear()
    {
        return new ElapsedTimeMessage(string.Empty);
    }

    /// <summary>
    /// Creates an elapsed time message with formatted elapsed time
    /// </summary>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="elapsed">Elapsed time</param>
    /// <returns>Formatted elapsed time message</returns>
    public static ElapsedTimeMessage Create(string operationName, TimeSpan elapsed)
    {
        var formattedTime = elapsed.TotalSeconds < 60
            ? $"{elapsed.Seconds:D2}.{elapsed.Milliseconds / 10:D2}s"
            : $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds / 10:D2}";

        return new ElapsedTimeMessage($"{operationName} - {formattedTime}");
    }
}