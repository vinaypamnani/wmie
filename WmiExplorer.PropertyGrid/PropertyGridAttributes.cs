namespace WmiExplorer.PropertyGrid
{
    /// <summary>
    /// Indicates that a property should be expanded by default in the property grid.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ExpandPropertyAttribute : Attribute
    {
    }
}
