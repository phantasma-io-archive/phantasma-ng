using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Exchange;

public struct TradingVolume : ISerializable
{
    public string Symbol0;
    public string Symbol1;
    public string Day;
    public BigInteger VolumeSymbol0;
    public BigInteger VolumeSymbol1;

    public TradingVolume(string Symbol0, string Symbol1, string Day, BigInteger VolumeSymbol0, BigInteger VolumeSymbol1)
    {
        this.Symbol0 = Symbol0;
        this.Symbol1 = Symbol1;
        this.Day = Day;
        this.VolumeSymbol0 = VolumeSymbol0;
        this.VolumeSymbol1 = VolumeSymbol1;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(Symbol0);
        writer.WriteVarString(Symbol1);
        writer.WriteVarString(Day);
        writer.WriteBigInteger(VolumeSymbol0);
        writer.WriteBigInteger(VolumeSymbol1);
    }

    public void UnserializeData(BinaryReader reader)
    {
        Symbol0 = reader.ReadVarString();
        Symbol1 = reader.ReadVarString();
        Day = reader.ReadVarString();
        VolumeSymbol0 = reader.ReadBigInteger();
        VolumeSymbol1 = reader.ReadBigInteger();
    }
}
