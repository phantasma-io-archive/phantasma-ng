using Microsoft.Extensions.Configuration;

namespace Phantasma.Node.Configuration;

public class SimulatorSettings
{
    public bool Enabled { get; }
    public bool Blocks { get; }

    public SimulatorSettings(IConfigurationSection section)
    {
        this.Enabled = section.GetValueEx<bool>("simulator.enabled");
        this.Blocks = section.GetValueEx<bool>("simulator.generate.blocks");
    }
}
