using System.ComponentModel;
using System.Management;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.Models;

/// <summary>
/// Model representing a WMI instance
/// </summary>
public class WmiBaseObject : IDisposable
{
    private readonly ManagementBaseObject _actualObject;
    private readonly object? _context;

    /// <summary>
    /// Constructor that takes required instance information
    /// </summary>
    /// <param name="actualObject">The underlying WMI object</param>
    public WmiBaseObject(ManagementBaseObject actualObject, object? context = null)
    {
        _actualObject = actualObject ?? throw new ArgumentNullException(nameof(actualObject));
        _context = context;
    }

    public ManagementBaseObject ActualObject => _actualObject;

    [ShowChildrenAsParent]
    public ManagementPath ClassPath => _actualObject.ClassPath;

    public object? Context => _context;

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