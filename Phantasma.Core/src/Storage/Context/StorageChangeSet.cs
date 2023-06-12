using System.IO;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Storage.Context
{
    public struct StorageChangeSetEntry : ISerializable
    {
        public byte[] oldValue;
        public byte[] newValue;
        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteByteArray(oldValue);
            writer.WriteByteArray(newValue);
        }

        public void UnserializeData(BinaryReader reader)
        {
            oldValue = reader.ReadByteArray();
            newValue = reader.ReadByteArray();
        }
    }
}
