using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Exchange.Structs;

public struct LPTokenContentRAM : ISerializable
{
    public BigInteger Amount0;
    public BigInteger Amount1;
    public BigInteger Liquidity;
    public BigInteger ClaimedFeesSymbol0;
    public BigInteger ClaimedFeesSymbol1;

    public LPTokenContentRAM(BigInteger Amount0, BigInteger Amount1, BigInteger Liquidity)
    {
        this.Amount0 = Amount0;
        this.Amount1 = Amount1;
        this.Liquidity = Liquidity;
        ClaimedFeesSymbol0 = 0;
        ClaimedFeesSymbol1 = 0;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteBigInteger(Amount0);
        writer.WriteBigInteger(Amount1);
        writer.WriteBigInteger(Liquidity);
        writer.WriteBigInteger(ClaimedFeesSymbol0);
        writer.WriteBigInteger(ClaimedFeesSymbol1);
    }

    public void UnserializeData(BinaryReader reader)
    {
        Amount0 = reader.ReadBigInteger();
        Amount1 = reader.ReadBigInteger();
        Liquidity = reader.ReadBigInteger();
        ClaimedFeesSymbol0 = reader.ReadBigInteger();
        ClaimedFeesSymbol1 = reader.ReadBigInteger();
    }
}
