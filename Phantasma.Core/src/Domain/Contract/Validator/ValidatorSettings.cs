using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Contract.Validator;

public class ValidatorSettings
{
    public Address Address { get; }
    public string Name { get; }
    public string Host { get; }
    public uint Port { get; }
    public string URL { get; }
    
    public ValidatorSettings(Address address, string name, string host, uint port)
    {
        this.Address = address;
        this.Name = name;
        this.Host = host;
        this.Port = port;
        this.URL = $"http://{host}:{port}";
    }
}
