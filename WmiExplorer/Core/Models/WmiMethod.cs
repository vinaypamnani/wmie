using System.ComponentModel;
using System.Management;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.Core.Models;

/// <summary>
/// Thin wrapper for a WMI MethodData object
/// </summary>
public class WmiMethod
{
    private readonly MethodData _methodData;
    private readonly ManagementClass? _parentClass;

    public WmiMethod(MethodData methodData, ManagementClass? parentClass = null)
    {
        _methodData = methodData ?? throw new ArgumentNullException(nameof(methodData));
        _parentClass = parentClass;
        InParameters = new WmiParameterCollection(_methodData.InParameters);
        OutParameters = new WmiParameterCollection(_methodData.OutParameters);
    }

    public string ClassName => _parentClass?["__Class"]?.ToString() ?? string.Empty;
    public string ClassPath => _parentClass?.Path?.Path ?? string.Empty;

    [Browsable(false)]
    public string Description
    {
        get
        {
            try
            {
                return _methodData.Qualifiers?["Description"]?.Value?.ToString() ?? string.Empty;
            }
            catch (ManagementException)
            {
                // Handle the case when the "Description" qualifier doesn't exist
                return string.Empty;
            }
        }
    }

    [Category("Parameters")]
    public WmiParameterCollection InParameters { get; }

    [Category("Method")]
    [Description("Indicates whether the method is static or whether it needs an instance for execution.")]
    public bool IsStatic
    {
        get
        {
            var qualifiers = Qualifiers;
            if (qualifiers == null)
                return false;
            var qualifier = qualifiers.Cast<QualifierData>().FirstOrDefault(q => q != null && q.Name != null && q.Name.Equals("static", StringComparison.OrdinalIgnoreCase));
            var value = qualifier?.Value?.ToString();
            return string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Category("Method")]
    public string Name => _methodData.Name;

    [Category("Method")]
    public string Origin => _methodData.Origin;

    [Category("Parameters")]
    public WmiParameterCollection OutParameters { get; }

    [Category("Qualifiers")]
    [ShowChildrenAsParent]
    public QualifierDataCollection Qualifiers => _methodData.Qualifiers;

    public override string ToString() => $"Static: {IsStatic}, InParameters: {InParameters.Count}, OutParameters: {OutParameters.Count}";
}