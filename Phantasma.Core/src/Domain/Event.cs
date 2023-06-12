using System.IO;
using System.Linq;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Numerics;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain
{
    public struct Event
    {
        public EventKind Kind { get; private set; }
        public Address Address { get; private set; }
        public string Contract { get; private set; }
        public byte[] Data { get; private set; }

        public Event(EventKind kind, Address address, string contract, byte[] data = null)
        {
            this.Kind = kind;
            this.Address = address;
            this.Contract = contract;
            this.Data = data;
        }

        public override string ToString()
        {
            return $"{Kind}/{Contract} @ {Address}: {Base16.Encode(Data)}";
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)this.Kind);
            writer.WriteAddress(this.Address);
            writer.WriteVarString(this.Contract);
            writer.WriteByteArray(this.Data);
        }

        public static Event Unserialize(BinaryReader reader)
        {
            var kind = (EventKind)reader.ReadByte();
            var address = reader.ReadAddress();
            var contract = reader.ReadVarString();
            var data = reader.ReadByteArray();
            return new Event(kind, address, contract, data);
        }

        public override bool Equals(object obj)
        {
            if (obj is not Event)
            {
                return false;
            }
            else if (obj is Event other)
            {
                if ( this.Data == null && other.Data == null)
                {
                    return this.Kind == other.Kind && this.Address.Text == other.Address.Text && this.Contract == other.Contract;
                }
                
                return this.Kind == other.Kind && this.Address.Text == other.Address.Text && this.Contract == other.Contract && this.Data.SequenceEqual(other.Data);
            }
            return base.Equals(obj);
        }
    }
}
