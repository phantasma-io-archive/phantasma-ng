using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;
using System.IO;
using System.Numerics;

namespace Phantasma.Core.Domain.Token.Structs;

public struct TokenInfusion: ISerializable
{
    public string Symbol;
    public BigInteger Value;

    public TokenInfusion(string symbol, BigInteger value)
    {
        Symbol = symbol;
        Value = value;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(Symbol);
        writer.WriteBigInteger(Value);
    }

    public void UnserializeData(BinaryReader reader)
    {
        Symbol = reader.ReadVarString();
        Value = reader.ReadBigInteger();
    }
}
