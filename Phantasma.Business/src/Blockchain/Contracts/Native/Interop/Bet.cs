using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Contracts.Native;

public struct Bet : ISerializable
{
    public Address Swapper;
    public string externalAddress;
    public string platform;
    public string symbol;
    public BigInteger BetFeeAmount;
    public Timestamp CreatedAt;
    public Timestamp UpdatedAt;
        
    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteAddress(Swapper);
        writer.WriteVarString(externalAddress);
        writer.WriteVarString(platform);
        writer.WriteVarString(symbol);
        writer.WriteBigInteger(BetFeeAmount);
        writer.WriteTimestamp(CreatedAt);
        writer.WriteTimestamp(UpdatedAt);
    }

    public void UnserializeData(BinaryReader reader)
    {
        Swapper = reader.ReadAddress();
        externalAddress = reader.ReadVarString();
        platform = reader.ReadVarString();
        symbol = reader.ReadVarString();
        BetFeeAmount = reader.ReadBigInteger();
        CreatedAt = reader.ReadTimestamp();
        UpdatedAt = reader.ReadTimestamp();
    }
}