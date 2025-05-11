namespace WmiExplorer.Services
{
    /// <summary>
    /// Service to handle clipboard operations
    /// </summary>
    public interface IApplicationService
    {
        /// <summary>
        /// Copies text to the clipboard
        /// </summary>
        void CopyToClipboard(string text);
    }
}