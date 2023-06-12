using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Interop;

public struct Bet : ISerializable
{
    public Address Swapper;
    public string ExternalAddress;
    public string Platform;
    public string Symbol;
    public BigInteger BetFeeAmount;
    public Timestamp CreatedAt;
    public Timestamp UpdatedAt;
        
    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteAddress(Swapper);
        writer.WriteVarString(ExternalAddress);
        writer.WriteVarString(Platform);
        writer.WriteVarString(Symbol);
        writer.WriteBigInteger(BetFeeAmount);
        writer.WriteTimestamp(CreatedAt);
        writer.WriteTimestamp(UpdatedAt);
    }

    public void UnserializeData(BinaryReader reader)
    {
        Swapper = reader.ReadAddress();
        ExternalAddress = reader.ReadVarString();
        Platform = reader.ReadVarString();
        Symbol = reader.ReadVarString();
        BetFeeAmount = reader.ReadBigInteger();
        CreatedAt = reader.ReadTimestamp();
        UpdatedAt = reader.ReadTimestamp();
    }
}