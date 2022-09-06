using System.IO;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain
{
    public struct OracleFeed: IFeed, ISerializable
    {
        public string Name { get; private set; }
        public Address Address { get; private set; }
        public FeedMode Mode { get; private set; }

        public OracleFeed(string name, Address address, FeedMode mode)
        {
            Name = name;
            Address = address;
            Mode = mode;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Name);
            writer.WriteAddress(Address);
            writer.Write((byte)Mode);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Name = reader.ReadVarString();
            Address = reader.ReadAddress();
            Mode = (FeedMode)reader.ReadByte();
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    SerializeData(writer);
                }

                return stream.ToArray();
            }
        }

        public static OracleFeed Unserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var entity = new OracleFeed();
                    entity.UnserializeData(reader);
                    return entity;
                }
            }
        }
    }
}
