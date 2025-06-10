namespace WmiExplorer.Common.Enums;

/// <summary>
/// Flags enum to represent different types of WMI classes to include in enumeration
/// </summary>
[Flags]
public enum WmiClassTypeFlags
{
    None = 0,
    CIM = 2,
    MSFT = 4,
    Perf = 8,
    All = CIM | MSFT | Perf
}