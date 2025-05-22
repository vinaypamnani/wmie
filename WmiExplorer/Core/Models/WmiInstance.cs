using System.ComponentModel;
using System.Management;

namespace WmiExplorer.Core.Models;

/// <summary>
/// Model representing a WMI instance
/// </summary>
public class WmiInstance
{
    private readonly ManagementObject _actualObject;

    /// <summary>
    /// Constructor that takes required instance information
    /// </summary>
    /// <param name="actualObject">The underlying WMI object</param>
    public WmiInstance(ManagementObject actualObject)
    {
        _actualObject = actualObject ?? throw new ArgumentNullException(nameof(actualObject));
    }

    [Browsable(false)]
    public ManagementObject ActualObject => _actualObject;

    public ManagementPath ClassPath => _actualObject.ClassPath;

    /// <summary>
    /// Gets the display name of the instance
    /// </summary>
    [Browsable(false)]
    public string InstanceName =>
                Path.RelativePath.ToString().Replace("\\\\", "\\") // TODO: Extract friendly name from known "Name" properties
                ?? string.Empty;

    public ObjectGetOptions Options => _actualObject.Options;

    public ManagementPath Path => _actualObject.Path;

    [Category("Properties")]
    public PropertyDataCollection Properties => _actualObject.Properties;

    [Category("Qualifiers")]
    public QualifierDataCollection Qualifiers => _actualObject.Qualifiers;

    public ManagementScope Scope => _actualObject.Scope;

    [Category("System Properties")]
    public PropertyDataCollection SystemProperties => _actualObject.SystemProperties;

    public object this[string propertyName] => _actualObject[propertyName];

    /// <summary>
    /// Returns the instance's string representation
    /// </summary>
    public override string ToString() => $"Instance: {InstanceName}";
}