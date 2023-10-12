using Microsoft.Extensions.Configuration;

namespace Phantasma.Node.Configuration;

public class AppSettings
{
    public bool UseShell { get; }
    public string AppName { get; }
    public bool NodeStart { get; }
    public string History { get; }
    public string Config { get; }
    public string Prompt { get; }

    public AppSettings(IConfigurationSection section)
    {
        this.UseShell = section.GetValueEx<bool>("shell.enabled");
        this.AppName = section.GetString("app.name");
        this.NodeStart = section.GetValueEx<bool>("node.start");
        this.History = section.GetString("history");
        this.Config = section.GetString("config");
        this.Prompt = section.GetString("prompt");
    }
}
