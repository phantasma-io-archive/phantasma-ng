using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Exchange.Structs;

public struct LPTokenContentROM : ISerializable
{
    public string Symbol0;
    public string Symbol1;
    public BigInteger ID;

    public LPTokenContentROM(string Symbol0, string Symbol1, BigInteger ID)
    {
        this.Symbol0 = Symbol0;
        this.Symbol1 = Symbol1;
        this.ID = ID;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(Symbol0);
        writer.WriteVarString(Symbol1);
        writer.WriteBigInteger(ID);
    }

    public void UnserializeData(BinaryReader reader)
    {
        Symbol0 = reader.ReadVarString();
        Symbol1 = reader.ReadVarString();
        ID = reader.ReadBigInteger();
    }
}
