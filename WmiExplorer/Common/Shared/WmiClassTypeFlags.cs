namespace WmiExplorer.Common.Shared
{
    /// <summary>
    /// Flags enum to represent different types of WMI classes to include in enumeration
    /// </summary>
    [Flags]
    public enum WmiClassTypeFlags
    {
        None = 0,
        System = 1,
        CIM = 2,
        MSFT = 4,
        Perf = 8,
        All = System | CIM | MSFT | Perf
    }
}