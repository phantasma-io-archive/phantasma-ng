using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Contracts.Native;

public struct CrossChainTransfer : ISerializable
{
    public CrossChainTransferStatus status;
    public bool FromExternalChain;
    public string Identifier;
    public Address FromUserAddress;
    public string UserExternalAddress;
    public Address Swapper;
    public string SwapperExternalAddress;
    public string Symbol;
    public BigInteger Amount;
    public BigInteger AmountSubFee;
    public BigInteger Fee;
    public string FromPlatform;
    public string ToPlatform;
    public Hash PhantasmaHash;
    public Hash ExternalHash;
    public Timestamp StartedAt;
    public Timestamp UpdatedAt;
    public Timestamp EndedAt;
        
    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarInt((int)status);
        writer.Write(FromExternalChain);
        writer.WriteVarString(Identifier);
        writer.WriteAddress(FromUserAddress);
        writer.WriteVarString(UserExternalAddress);
        writer.WriteAddress(Swapper);
        writer.WriteVarString(SwapperExternalAddress);
        writer.WriteVarString(Symbol);
        writer.WriteBigInteger(Amount);
        writer.WriteBigInteger(AmountSubFee);
        writer.WriteBigInteger(Fee);
        writer.WriteVarString(FromPlatform);
        writer.WriteVarString(ToPlatform);
        writer.WriteHash(PhantasmaHash);
        writer.WriteHash(ExternalHash);
        writer.WriteTimestamp(StartedAt);
        writer.WriteTimestamp(UpdatedAt);
        writer.WriteTimestamp(EndedAt);
    }

    public void UnserializeData(BinaryReader reader)
    {
        this.status = (CrossChainTransferStatus)reader.ReadVarInt();
        this.FromExternalChain = reader.ReadBoolean();
        this.Identifier = reader.ReadVarString();
        this.FromUserAddress = reader.ReadAddress();
        this.UserExternalAddress = reader.ReadVarString();
        this.Swapper = reader.ReadAddress();
        this.SwapperExternalAddress = reader.ReadVarString();
        this.Symbol = reader.ReadVarString();
        this.Amount = reader.ReadBigInteger();
        this.AmountSubFee = reader.ReadBigInteger();
        this.Fee = reader.ReadBigInteger();
        this.FromPlatform = reader.ReadVarString();
        this.ToPlatform = reader.ReadVarString();
        this.PhantasmaHash = reader.ReadHash();
        this.ExternalHash = reader.ReadHash();
        this.StartedAt = reader.ReadTimestamp();
        this.UpdatedAt = reader.ReadTimestamp();
        this.EndedAt = reader.ReadTimestamp();
    }
}
