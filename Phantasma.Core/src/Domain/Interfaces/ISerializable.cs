using System.IO;

namespace Phantasma.Core.Domain.Interfaces;

public interface ISerializable
{
    public void SerializeData(BinaryWriter writer);
    public void UnserializeData(BinaryReader reader);
}
