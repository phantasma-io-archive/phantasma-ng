using System.IO;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Interop.Structs;

public struct PlatformTokens : ISerializable
{
    public string Symbol;
    public int Decimals;
    public string ExternalContractAddress;
    public Address LocalContractAddress;
    public Address LocalAddress;
    public string ExternalAddress;
        
    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(this.Symbol);
        writer.WriteVarInt(this.Decimals);
        writer.WriteVarString(this.ExternalContractAddress);
        writer.WriteAddress(LocalContractAddress);
        writer.WriteAddress(LocalAddress);
        writer.WriteVarString(ExternalAddress);
    }

    public void UnserializeData(BinaryReader reader)
    {
        this.Symbol = reader.ReadVarString();
        this.Decimals = (int)reader.ReadVarInt();
        this.ExternalContractAddress = reader.ReadVarString();
        this.LocalContractAddress = reader.ReadAddress();
        this.LocalAddress = reader.ReadAddress();
        this.ExternalAddress = reader.ReadVarString();
    }
}
