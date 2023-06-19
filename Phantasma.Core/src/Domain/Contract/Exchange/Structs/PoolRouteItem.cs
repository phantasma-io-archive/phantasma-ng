using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Exchange.Structs;

public struct PoolRouteItem : ISerializable
{
    public string FromSymbol;
    public string ToSymbol;
    public BigInteger AmountIn;
    public BigInteger AmountOut;

    public PoolRouteItem(string fromSymbol, string toSymbol, BigInteger amountIn, BigInteger amountOut)
    {
        this.FromSymbol = fromSymbol;
        this.ToSymbol = toSymbol;
        this.AmountIn = amountIn;
        this.AmountOut = amountOut;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(FromSymbol);
        writer.WriteVarString(ToSymbol);
        writer.WriteBigInteger(AmountIn);
        writer.WriteBigInteger(AmountOut);
    }

    public void UnserializeData(BinaryReader reader)
    {
        this.FromSymbol = reader.ReadVarString();
        this.ToSymbol = reader.ReadVarString();
        this.AmountIn = reader.ReadBigInteger();
        this.AmountOut = reader.ReadBigInteger();
    }
}
