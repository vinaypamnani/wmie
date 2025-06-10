using System.Collections.ObjectModel;
using WmiExplorer.Common.Base;

namespace WmiExplorer.Presentation.ViewModels.Watcher;

/// <summary>
/// Manages the display property list by coordinating between event properties and target class properties
/// </summary>
public class DisplayPropertyManager : DisposableObservableObject
{
    private readonly ObservableCollection<PropertyDisplayInfo> _displayProperties = new();

    /// <summary>
    /// Initializes a new instance of the DisplayPropertyManager class
    /// </summary>
    public DisplayPropertyManager()
    {
        Properties = new ReadOnlyObservableCollection<PropertyDisplayInfo>(_displayProperties);
    }

    /// <summary>
    /// Gets the read-only collection of display properties
    /// </summary>
    public ReadOnlyObservableCollection<PropertyDisplayInfo> Properties { get; }

    /// <summary>
    /// Clears all display properties
    /// </summary>
    public void Clear()
    {
        _displayProperties.Clear();
    }

    /// <summary>
    /// Gets the default display property from the current collection
    /// </summary>
    /// <returns>The first property in the collection or a default __RELPATH property</returns>
    public PropertyDisplayInfo GetDefaultDisplayProperty()
    {
        return _displayProperties.FirstOrDefault() ??
               new PropertyDisplayInfo { Name = "__RELPATH", Type = "string" };
    }

    /// <summary>
    /// Updates the display property list based on the query builder state
    /// </summary>
    /// <param name="queryBuilder">The query builder containing current state</param>
    /// <param name="eventProperties">The event property manager</param>
    /// <param name="targetClassProperties">The target class property manager</param>
    public void UpdateDisplayPropertyList(
        WatcherQueryBuilder queryBuilder,
        PropertyListManager eventProperties,
        PropertyListManager targetClassProperties)
    {
        _displayProperties.Clear();

        if (queryBuilder.IsTargetClassEnabled &&
            queryBuilder.IsTargetClassPropertyEnabled &&
            queryBuilder.EventType != WatcherQueryBuilder.WmiEventType.Method)
        {
            // Use target class properties
            foreach (var prop in targetClassProperties.Properties)
            {
                _displayProperties.Add(prop);
            }
        }
        else if (!queryBuilder.IsIntrinsicEvent)
        {
            // Use event properties
            foreach (var prop in eventProperties.Properties)
            {
                _displayProperties.Add(prop);
            }
        }
        else
        {
            // Use default intrinsic property
            _displayProperties.Add(new PropertyDisplayInfo
            {
                Name = "__RELPATH",
                Type = "string"
            });
        }
    }

    /// <summary>
    /// Disposes the manager and clears all properties
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Clear();
        }
        base.Dispose(disposing);
    }
}