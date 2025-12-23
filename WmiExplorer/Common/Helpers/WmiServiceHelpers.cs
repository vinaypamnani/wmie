using System.Management;

namespace WmiExplorer.Common.Helpers;

public static class WmiServiceHelpers
{
    public static string ExtractWmiErrorInformation(ManagementBaseObject? statusObject, ManagementStatus status)
    {
        try
        {
            var errorCode = status.ToString() ?? ManagementStatus.Failed.ToString();
            var statusMessage = $"ErrorCode: {errorCode}";

            if (statusObject == null || statusObject.Properties == null || statusObject.Properties.Count == 0)
            {
                return statusMessage;
            }

            string? messageId = null;
            string? message = null;
            string? windowsErrorMessage = null;
            try { messageId = statusObject.Properties["MessageID"]?.Value?.ToString(); }
            catch (Exception) { /* Property access failed, continue without messageId */ }
            try { message = statusObject.Properties["Message"]?.Value?.ToString(); }
            catch (Exception) { /* Property access failed, continue without message */ }
            try { windowsErrorMessage = statusObject.Properties["error_WindowsErrorMessage"]?.Value?.ToString(); }
            catch (Exception) { /* Property access failed, continue without windowsErrorMessage */ }

            var errorDetails = new List<string>();

            if (!string.IsNullOrWhiteSpace(messageId))
                errorDetails.Add($"MessageID: {messageId}");

            if (!string.IsNullOrWhiteSpace(windowsErrorMessage))
                errorDetails.Add($"Windows Error: {windowsErrorMessage}");

            if (!string.IsNullOrWhiteSpace(message))
                errorDetails.Add($"Message: {message}");

            if (errorDetails.Any())
                return $"{string.Join(", ", errorDetails)}";
            else
                return statusMessage;
        }
        catch (Exception ex)
        {
            // Never throw from here; return a fallback message
            return status.ToString() ?? $"WMI error details unavailable: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public static string ExtractWmiErrorMessage(ManagementException managementException)
    {
        try
        {
            var status = managementException?.ErrorCode ?? ManagementStatus.Failed;
            if (managementException?.ErrorInformation != null)
            {
                return ExtractWmiErrorInformation(managementException.ErrorInformation, status);
            }

            if (!string.IsNullOrWhiteSpace(managementException?.Message))
            {
                return managementException.Message;
            }
            var baseMessage = $"ErrorCode: {managementException?.ErrorCode ?? ManagementStatus.Failed}";
            return baseMessage;
        }
        catch (Exception ex)
        {
            // Never throw from here; return a fallback message
            return managementException?.Message ?? $"WMI error message unavailable: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>
    /// Formats a namespace path for user-friendly display
    /// </summary>
    /// <param name="namespacePath">The full namespace path (e.g., \\.\root\cimv2)</param>
    /// <returns>A user-friendly namespace name (e.g., root\cimv2)</returns>
    public static string FormatNamespaceForDisplay(string namespacePath)
    {
        if (string.IsNullOrWhiteSpace(namespacePath))
            return "root";

        // Remove computer part (\\computer\ or \\.\ for local)
        if (namespacePath.StartsWith(@"\\"))
        {
            var segments = namespacePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                // Return everything after the computer name
                return string.Join("\\", segments.Skip(1));
            }
        }

        return namespacePath;
    }
}