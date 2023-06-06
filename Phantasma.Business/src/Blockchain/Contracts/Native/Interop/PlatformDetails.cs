using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Contracts.Native;

public struct PlatformDetails : ISerializable
{
    public string Name;
    public string MainSymbol;
    public string FuelSymbol;
    public int Decimals;
    public Address Owner;
    public Address LocalAddress;
    public string ExternalAddress;
    public bool IsSwapEnabled;
    public PlatformTokens[] Tokens;
    public bool MainSwapper;
    public BigInteger FeePercentage;
        
    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(Name);
        writer.WriteVarString(MainSymbol);
        writer.WriteVarString(FuelSymbol);
        writer.WriteVarInt(Decimals);
        writer.WriteAddress(Owner);
        writer.WriteAddress(LocalAddress);
        writer.WriteVarString(ExternalAddress);
        writer.Write(IsSwapEnabled);
        writer.WriteVarInt(Tokens.Length);
        foreach (var token in Tokens)
        {
            token.SerializeData(writer);
        }
        writer.Write(MainSwapper);
        writer.WriteBigInteger(FeePercentage);
    }

    public void UnserializeData(BinaryReader reader)
    {
        this.Name = reader.ReadVarString();
        this.MainSymbol = reader.ReadVarString();
        this.FuelSymbol = reader.ReadVarString();
        this.Decimals = (int)reader.ReadVarInt();
        this.Owner = reader.ReadAddress();
        this.LocalAddress = reader.ReadAddress();
        this.ExternalAddress = reader.ReadVarString();
        this.IsSwapEnabled = reader.ReadBoolean();
        var tokenCount = (int)reader.ReadVarInt();
        this.Tokens = new PlatformTokens[tokenCount];
        for (int i = 0; i < tokenCount; i++)
        {
            var temp = new PlatformTokens();
            temp.UnserializeData(reader);
            this.Tokens[i] = temp;
        }
        this.MainSwapper = reader.ReadBoolean();
        this.FeePercentage = reader.ReadBigInteger();
    }
}
