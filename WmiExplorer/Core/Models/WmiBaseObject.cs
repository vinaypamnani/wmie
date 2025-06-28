using System.ComponentModel;
using System.Management;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.Core.Models;

/// <summary>
/// Model representing a WMI instance
/// </summary>
public class WmiBaseObject : IDisposable
{
    private readonly ManagementBaseObject _actualObject;

    /// <summary>
    /// Constructor that takes required instance information
    /// </summary>
    /// <param name="actualObject">The underlying WMI object</param>
    public WmiBaseObject(ManagementBaseObject actualObject)
    {
        _actualObject = actualObject ?? throw new ArgumentNullException(nameof(actualObject));
    }

    [Browsable(false)]
    public ManagementBaseObject ActualObject => _actualObject;

    [ShowChildrenAsParent]
    public ManagementPath ClassPath => _actualObject.ClassPath;

    [Category("Properties")]
    [ShowChildrenAsParent]
    public PropertyDataCollection Properties => _actualObject.Properties;

    [Category("System Properties")]
    [ShowChildrenAsParent]
    [Browsable(false)]
    public PropertyDataCollection SystemProperties => _actualObject.SystemProperties;

    public object this[string propertyName] => _actualObject[propertyName];

    #region IDisposable

    public void Dispose()
    {
        _actualObject?.Dispose();
    }

    #endregion
}