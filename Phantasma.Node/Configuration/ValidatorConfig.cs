using Microsoft.Extensions.Configuration;
using Phantasma.Core.Cryptography;

namespace Phantasma.Node;

public class ValidatorConfig
{
    public Address Address { get; }
    public string Name { get; }
    public string Host { get; }
    public uint Port { get; }
    public string URL { get; }
        
    public ValidatorConfig(IConfigurationSection section)
    {
        this.Address = Address.FromText(section.GetString("validator.address"));
        this.Name = section.GetString("validator.name");
        this.Host = section.GetString("validator.api.host");
        this.Port = section.GetValueEx<uint>("validator.api.port");
        this.URL = Host + ":" + Port;
    }
}
