using System;
using Microsoft.Extensions.Configuration;

namespace Phantasma.Node;

public class RPCSettings
{
    public string Address { get; }
    public uint Port { get; }

    public RPCSettings(IConfigurationSection section)
    {
        this.Address = section.GetString("rpc.address");
        this.Port = section.GetValueEx<UInt32>("rpc.port");
    }
}
