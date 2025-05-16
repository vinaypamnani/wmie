using System.Management;
using System.Collections.Generic;
using System.Linq;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Thin wrapper for a WMI MethodData object
    /// </summary>
    public class WmiMethod
    {
        private readonly MethodData _methodData;

        public WmiMethod(MethodData methodData)
        {
            _methodData = methodData ?? throw new ArgumentNullException(nameof(methodData));
            InParameters = new WmiParameterCollection(_methodData.InParameters);
            OutParameters = new WmiParameterCollection(_methodData.OutParameters);
        }

        public string Name => _methodData.Name;
        public QualifierDataCollection Qualifiers => _methodData.Qualifiers;
        public string Origin => _methodData.Origin;
        public WmiParameterCollection InParameters { get; }
        public WmiParameterCollection OutParameters { get; }

        public string Description => _methodData.Qualifiers?["Description"]?.Value?.ToString() ?? string.Empty;

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

        public override string ToString() => $"Static: {IsStatic}, InParameters: {InParameters.Count}, OutParameters: {OutParameters.Count}";
    }
}
