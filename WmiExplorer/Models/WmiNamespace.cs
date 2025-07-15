using System.ComponentModel;
using System.Management;

namespace WmiExplorer.Models;

/// <summary>
/// Minimal container for a WMI namespace object
/// </summary>
public class WmiNamespace : IDisposable
{
    private ManagementObject _actualObject;

    /// <summary>
    /// Constructor for a WMI namespace, optionally with ConnectionOptions (root if specified)
    /// </summary>
    public WmiNamespace(ManagementObject actualObject, string namespacePath, ConnectionOptions connectionOptions)
    {
        _actualObject = actualObject;
        NamespacePath = namespacePath ?? throw new ArgumentNullException(nameof(namespacePath));
        ConnectionOptions = connectionOptions;
        IsRoot = true;
    }

    /// <summary>
    /// Constructor for a child WMI namespace, propagating ConnectionOptions from the parent
    /// </summary>
    public WmiNamespace(ManagementObject actualObject, string namespacePath, WmiNamespace parent)
    {
        _actualObject = actualObject;
        NamespacePath = namespacePath ?? throw new ArgumentNullException(nameof(namespacePath));
        ConnectionOptions = parent.ConnectionOptions;
        IsRoot = false;
    }

    [Browsable(false)]
    public ManagementObject ActualObject => _actualObject;

    /// <summary>
    /// The ConnectionOptions used for this namespace
    /// </summary>
    [Category("Namespace")]
    public ConnectionOptions ConnectionOptions { get; }

    [Category("Scope")]
    public bool IsConnected { get; set; } = false;

    /// <summary>
    /// Indicates whether this namespace is the root namespace (if ConnectionOptions is specified)
    /// </summary>
    [Browsable(false)]
    public bool IsRoot { get; }

    /// <summary>
    /// The name of the namespace (last segment after the last backslash)
    /// </summary>
    [Category("Namespace")]
    public string NamespaceName => string.IsNullOrEmpty(NamespacePath) ? string.Empty : NamespacePath.Contains("\\")
                                        ? NamespacePath.Substring(NamespacePath.LastIndexOf("\\") + 1) : NamespacePath;

    /// <summary>
    /// The path of the namespace (e.g., "root\\cimv2")
    /// </summary>
    [Category("Namespace")]
    public string NamespacePath { get; }

    [Category("Qualifiers")]
    public QualifierDataCollection Qualifiers => _actualObject.Qualifiers;

    /// <summary>
    /// The relative path of the namespace (strips out \\computername\ or \\.\ from the start)
    /// </summary>
    [Category("Namespace")]
    public string RelativePath
    {
        get
        {
            if (string.IsNullOrEmpty(NamespacePath))
                return string.Empty;

            // Remove leading \\computername\ or \\.\
            var path = NamespacePath;
            if (path.StartsWith("\\\\.\\", StringComparison.Ordinal))
                return path.Substring(4);
            if (path.StartsWith("\\\\", StringComparison.Ordinal))
            {
                int idx = path.IndexOf("\\", 2);
                if (idx > 1 && idx + 1 < path.Length)
                    return path.Substring(idx + 1);
                return string.Empty;
            }
            return path;
        }
    }

    /// <summary>
    /// Returns the string representation
    /// </summary>
    public override string ToString() => $"Namespace: {NamespacePath}";

    #region IDisposable

    public void Dispose()
    {
        _actualObject?.Dispose();
    }

    #endregion
}