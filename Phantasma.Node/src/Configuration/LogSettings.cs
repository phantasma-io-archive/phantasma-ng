using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace Phantasma.Node.Configuration;

public class LogSettings
{
    public string LogName { get; }
    public string LogPath { get; }
    public LogEventLevel FileLevel { get; }
    public LogEventLevel ShellLevel { get; }

    public LogSettings(IConfigurationSection section)
    {
        this.LogName = section.GetString("file.name", "spook.log");
        this.LogPath = section.GetString("file.path", Path.GetTempPath());
        this.FileLevel = section.GetValueEx<LogEventLevel>("file.level", LogEventLevel.Verbose);
        this.ShellLevel = section.GetValueEx<LogEventLevel>("shell.level", LogEventLevel.Information);
    }
}
