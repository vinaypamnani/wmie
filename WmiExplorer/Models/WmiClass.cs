using System.ComponentModel;
using System.Management;
using WmiExplorer.PropertyGrid;
using WmiExplorer.Services;

namespace WmiExplorer.Models;

/// <summary>
/// Thin wrapper for a WMI class ManagementClass
/// </summary>
public class WmiClass : IDisposable
{
    private ManagementClass _actualClass;
    private ManagementObject? _cachedProvider;
    private bool _providerCacheInitialized;
    private readonly IWmiService? _wmiService;

    public WmiClass(ManagementBaseObject actualClass, IWmiService? wmiService = null)
    {
        _actualClass = (ManagementClass)actualClass ?? throw new ArgumentNullException(nameof(actualClass));
        _wmiService = wmiService;

        // Initialize the Methods collection and populate it with WmiMethod objects, encapsulating the MethodData objects so retrieval is fast.
        // This is done in the constructor to avoid having to do it in the property getter, which would be slow.
        Methods = new List<WmiMethod>();
        foreach (MethodData method in _actualClass.Methods)
        {
            Methods.Add(new WmiMethod(method, _actualClass));
        }
    }

    [Browsable(false)]
    public ManagementClass ActualClass => _actualClass;

    [Category("Class")]
    public string ClassName => _actualClass["__Class"]?.ToString() ?? string.Empty;

    [Category("Class")]
    public ManagementPath ClassPath => _actualClass.ClassPath;

    [Browsable(false)]
    public string Description
    {
        get
        {
            try
            {
                if (_actualClass.Qualifiers != null && _actualClass.Qualifiers.Cast<QualifierData>().Any(q => q.Name == "Description"))
                    return _actualClass.Qualifiers["Description"]?.Value?.ToString() ?? string.Empty;
            }
            catch
            {
                // Optionally log the error
            }
            return string.Empty;
        }
    }

    [Browsable(false)]
    public string LocalRelativePath => _actualClass.Path.NamespacePath + ":" + _actualClass.Path.ClassName;

    [Category("Methods")]
    public List<WmiMethod> Methods { get; }

    [Category("Class")]
    public string NamespacePath => _actualClass.Scope.Path.Path ?? string.Empty;

    [Category("Class")]
    [Browsable(false)]
    public ObjectGetOptions Options => _actualClass.Options;

    [Category("Class")]
    public ManagementPath Path => _actualClass.Path;

    [Category("Properties")]
    public PropertyDataCollection Properties => _actualClass.Properties;

    /// <summary>
    /// Gets the provider instance associated with this class from the WmiService cache, if any
    /// </summary>
    [Category("Provider")]
    [ShowChildrenAsParent]
    public ManagementObject? Provider
    {
        get
        {
            // Return cached value if already initialized
            if (_providerCacheInitialized)
                return _cachedProvider;

            // Initialize cache if not already done
            if (_wmiService == null || _actualClass?.Qualifiers == null)
            {
                _providerCacheInitialized = true;
                _cachedProvider = null;
                return null;
            }

            try
            {
                _cachedProvider = _wmiService.GetCachedProviderForClass(NamespacePath, ClassName, _actualClass);
                _providerCacheInitialized = true;
                return _cachedProvider;
            }
            catch
            {
                // Return null if there's any error accessing the provider
                _providerCacheInitialized = true;
                _cachedProvider = null;
                return null;
            }
        }
    }

    [Category("Qualifiers")]
    public QualifierDataCollection Qualifiers => _actualClass.Qualifiers;

    [Category("Class")]
    public ManagementScope Scope => _actualClass.Scope;

    [Category("Class")]
    public string SuperClass => (_actualClass.Derivation != null && _actualClass.Derivation.Count > 0 && _actualClass.Derivation[0] != null) ? _actualClass.Derivation[0]! : string.Empty;

    [Category("Properties")]
    public PropertyDataCollection SystemProperties => _actualClass.SystemProperties;

    [Category("Class")]
    public string[]
     Derivation => _actualClass.Derivation?.Cast<string>().ToArray() ?? new string[0];

    public override string ToString()
    {
        return $"Class: {LocalRelativePath}";
    }

    #region IDisposable

    public void Dispose()
    {
        _actualClass?.Dispose();
    }

    #endregion
}