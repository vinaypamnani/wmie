namespace WmiExplorer.Presentation.ViewModels.Watcher;

public class PropertyDisplayInfo
{
    public string Display => string.IsNullOrEmpty(Type) ? Name : $"{Name} [{Type}]";
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    public override string ToString() => Display;
}