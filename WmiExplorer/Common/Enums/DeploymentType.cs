namespace WmiExplorer.Common.Enums;

/// <summary>
/// Specifies the deployment type of the application.
/// </summary>
public enum DeploymentType
{
    /// <summary>
    /// Single-file deployment without embedded runtime (requires .NET runtime installed).
    /// </summary>
    SingleFile,

    /// <summary>
    /// Standalone single-file deployment with embedded .NET runtime.
    /// </summary>
    Standalone
}