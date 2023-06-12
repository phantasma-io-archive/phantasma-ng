using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Exchange;

public struct Pool : ISerializable
{
    public string Symbol0; // Symbol
    public string Symbol1; // Pair
    public string Symbol0Address;
    public string Symbol1Address;
    public BigInteger Amount0;
    public BigInteger Amount1;
    public BigInteger FeeRatio;
    public BigInteger TotalLiquidity;
    public BigInteger FeesForUsersSymbol0;
    public BigInteger FeesForUsersSymbol1;
    public BigInteger FeesForOwnerSymbol0;
    public BigInteger FeesForOwnerSymbol1;


    public Pool(string Symbol0, string Symbol1, string Symbol0Address, string Symbol1Address, BigInteger Amount0, BigInteger Amount1, BigInteger FeeRatio, BigInteger TotalLiquidity)
    {
        this.Symbol0 = Symbol0;
        this.Symbol1 = Symbol1;
        this.Symbol0Address = Symbol0Address;
        this.Symbol1Address = Symbol1Address;
        this.Amount0 = Amount0;
        this.Amount1 = Amount1;
        this.FeeRatio = FeeRatio;
        this.TotalLiquidity = TotalLiquidity;
        FeesForUsersSymbol0 = 0;
        FeesForUsersSymbol1 = 0;
        FeesForOwnerSymbol0 = 0;
        FeesForOwnerSymbol1 = 0;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(Symbol0);
        writer.WriteVarString(Symbol1);
        writer.WriteVarString(Symbol0Address);
        writer.WriteVarString(Symbol1Address);
        writer.WriteBigInteger(Amount0);
        writer.WriteBigInteger(Amount1);
        writer.WriteBigInteger(FeeRatio);
        writer.WriteBigInteger(TotalLiquidity);
        writer.WriteBigInteger(FeesForUsersSymbol0);
        writer.WriteBigInteger(FeesForUsersSymbol1);
        writer.WriteBigInteger(FeesForOwnerSymbol0);
        writer.WriteBigInteger(FeesForOwnerSymbol1);
    }

    public void UnserializeData(BinaryReader reader)
    {
        Symbol0 = reader.ReadVarString();
        Symbol1 = reader.ReadVarString();
        Symbol0Address = reader.ReadVarString();
        Symbol1Address = reader.ReadVarString();
        Amount0 = reader.ReadBigInteger();
        Amount1 = reader.ReadBigInteger();
        FeeRatio = reader.ReadBigInteger();
        TotalLiquidity = reader.ReadBigInteger();
        FeesForUsersSymbol0 = reader.ReadBigInteger();
        FeesForUsersSymbol1 = reader.ReadBigInteger();
        FeesForOwnerSymbol0 = reader.ReadBigInteger();
        FeesForOwnerSymbol1 = reader.ReadBigInteger();
    }
}
