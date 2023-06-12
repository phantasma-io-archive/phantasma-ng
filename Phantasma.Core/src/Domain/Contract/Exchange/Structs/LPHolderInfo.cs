using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Exchange.Structs;

public struct LPHolderInfo : ISerializable
{
    public BigInteger NFTID;
    public BigInteger UnclaimedSymbol0;
    public BigInteger UnclaimedSymbol1;
    public BigInteger ClaimedSymbol0;
    public BigInteger ClaimedSymbol1;

    public LPHolderInfo(BigInteger NFTID, BigInteger unclaimedSymbol0, BigInteger unclaimedSymbol1, BigInteger claimedSymbol0, BigInteger claimedSymbol1)
    {
        this.NFTID = NFTID;
        UnclaimedSymbol0 = unclaimedSymbol0;
        UnclaimedSymbol1 = unclaimedSymbol1;
        ClaimedSymbol0 = claimedSymbol0;
        ClaimedSymbol1 = claimedSymbol1;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteBigInteger(NFTID);
        writer.WriteBigInteger(UnclaimedSymbol0);
        writer.WriteBigInteger(UnclaimedSymbol1);
        writer.WriteBigInteger(ClaimedSymbol0);
        writer.WriteBigInteger(ClaimedSymbol1);
    }

    public void UnserializeData(BinaryReader reader)
    {
        NFTID = reader.ReadBigInteger();
        UnclaimedSymbol0 = reader.ReadBigInteger();
        UnclaimedSymbol1 = reader.ReadBigInteger();
        ClaimedSymbol0 = reader.ReadBigInteger();
        ClaimedSymbol1 = reader.ReadBigInteger();
    }
}
