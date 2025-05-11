using System.ComponentModel;
using System.Management;

namespace WmiExplorer.Core.WmiWrappers
{
    /// <summary>
    /// Wrapper Class for ManagementObject
    /// </summary>
    [TypeConverter(typeof(ManagementBaseObjectWConverter))]
    public class ManagementObjectW : ManagementBaseObjectW
    {
        public ManagementObjectW(ManagementBaseObject actualObject)
            : base(actualObject)
        {
        }
    }
}