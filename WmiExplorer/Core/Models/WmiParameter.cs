using System.Collections;

namespace WmiExplorer.Core.Models;

public class WmiParameter
{
    public int Id => GetQualifierValue("Id") is int id ? id : -1;
    public bool IsArray { get; set; }
    public bool IsLocal { get; set; }
    public string? Name { get; set; }
    public string? Origin { get; set; }
    public System.Management.QualifierDataCollection? Qualifiers { get; set; }
    public string? Type { get; set; }
    public object? Value { get; set; }
    public string? Description => GetQualifierValue("Description") as string;

    public override string ToString() => Name ?? string.Empty;

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