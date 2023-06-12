using System.IO;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Events;

public class ContractEvent
{
    public readonly byte value;
    public readonly string name;
    public readonly VMType returnType;
    public readonly byte[] description;

    public ContractEvent(byte value, string name, VMType returnType, byte[] description)
    {
        this.value = value;
        this.name = name;
        this.returnType = returnType;
        this.description = description;
    }

    public override string ToString()
    {
        return $"{name} : {returnType} => {value}";
    }

    public static ContractEvent Unserialize(BinaryReader reader)
    {
        var value = reader.ReadByte();
        var name = reader.ReadVarString();
        var returnType = (VMType)reader.ReadByte();
        var description = reader.ReadByteArray();

        return new ContractEvent(value, name, returnType, description);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)value);
        writer.WriteVarString(name);
        writer.Write((byte)returnType);
        writer.WriteByteArray(description);

    }
}
