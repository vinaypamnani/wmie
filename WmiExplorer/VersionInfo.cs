using System;
using System.Reflection;

namespace WmiExplorer
{
    /// <summary>
    /// Provides application version information.
    /// </summary>
    public static class VersionInfo
    {
        public static string AppVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version != null ? $"{version}" : "Unknown";
            }
        }
    }
}
