using System.Collections;
using System.ComponentModel;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.Models;

public class WmiParameter
{
    [Category("Parameter")]
    public string? CimType => GetQualifierValue("CIMTYPE") as string;

    [Browsable(false)]
    public string? Description => GetQualifierValue("Description") as string;

    [Category("Parameter")]
    public int Id => GetQualifierValue("Id") is int id ? id : -1;

    [Category("Parameter")]
    public bool IsArray { get; set; }

    [Category("Parameter")]
    public bool IsLocal { get; set; }

    /// <summary>
    /// Indicates whether this parameter is optional based on the 'optional' qualifier.
    /// </summary>
    [Category("Parameter")]
    [Description("Indicates whether this parameter is optional based on the 'optional' qualifier. This is not guaranteed to be accurate for all WMI methods, as some may not use the 'optional' qualifier.")]
    public bool HasOptionalQualifier
    {
        get
        {
            if (Qualifiers == null) return false;
            foreach (System.Management.QualifierData qualifier in Qualifiers)
            {
                if (qualifier.Name.Equals("optional", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    [Category("Parameter")]
    public string? Name { get; set; }

    [Category("Parameter")]
    public string? Origin { get; set; }

    [Category("Qualifiers")]
    [ShowChildrenAsParent]
    public System.Management.QualifierDataCollection? Qualifiers { get; set; }

    [Category("Parameter")]
    public string? Type { get; set; }

    [Category("Parameter")]
    public object? Value { get; set; }

    public override string ToString() => $"Method Parameter: {Name}" ?? string.Empty;

    private object? GetQualifierValue(string qualifierName)
    {
        if (Qualifiers == null) return null;
        foreach (System.Management.QualifierData qualifier in Qualifiers)
        {
            if (qualifier.Name.Equals(qualifierName, StringComparison.OrdinalIgnoreCase))
            {
                return qualifier.Value;
            }
        }
        return null;
    }
}

/// <summary>
/// Collection of WMI method parameters, with DTO for each parameter
/// </summary>
public class WmiParameterCollection : IEnumerable<WmiParameter>
{
    private readonly List<WmiParameter> _parameters = new();

    public WmiParameterCollection(System.Management.ManagementBaseObject? baseObject)
    {
        if (baseObject != null)
        {
            foreach (System.Management.PropertyData prop in baseObject.Properties)
            {
                _parameters.Add(new WmiParameter
                {
                    Name = prop.Name,
                    Type = prop.Type.ToString(),
                    Value = prop.Value,
                    IsArray = prop.IsArray,
                    Qualifiers = prop.Qualifiers,
                    IsLocal = prop.IsLocal,
                    Origin = prop.Origin
                });
            }
        }
    }

    public int Count => _parameters.Count;
    public WmiParameter this[int index] => _parameters[index];

    public override string ToString() => $"Collection (Count: {_parameters.Count})";

    #region interfaces
    public IEnumerator<WmiParameter> GetEnumerator() => _parameters.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    #endregion
}