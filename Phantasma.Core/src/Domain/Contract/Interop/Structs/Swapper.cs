using System.IO;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Interop.Structs;

public struct Swapper : ISerializable
{
    public Address InternalAddress;
    public string ExternalAddress;
    public bool IsActive;

        
    public Swapper(Address internalAddress, string externalAddress, bool isActive)
    {
        this.InternalAddress = internalAddress;
        this.ExternalAddress = externalAddress;
        this.IsActive = isActive;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteAddress(InternalAddress);
        writer.WriteVarString(ExternalAddress);
        writer.Write(IsActive);
    }

    public void UnserializeData(BinaryReader reader)
    {
        this.InternalAddress = reader.ReadAddress();
        this.ExternalAddress = reader.ReadVarString();
        this.IsActive = reader.ReadBoolean();
    }
}
