using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain;

public sealed class TransactionV2 : ITransaction, ISerializable
{
    public readonly static TransactionV2 Null = null;
    public byte[] Script { get; private set; }
    public string NexusName { get; private set; }
    public string ChainName { get; private set; }
    public Timestamp Expiration { get; private set; }
    public byte[] Payload { get; private set; }
    public Signature[] Signatures { get; private set; }
    public Hash Hash { get; private set; }
    public Address GasPayer { get; private set; }
    public Address GasTarget { get; private set; }
    public BigInteger GasLimit { get; private set; }
    public BigInteger GasPrice { get; private set; }
    public bool HasSignatures => Signatures != null && Signatures.Length > 0;

    #region Constructors
    // required for deserialization
    public TransactionV2()
    {
        this.Hash = Hash.Null;
    }
    
    public TransactionV2(
        string nexusName,
        string chainName,
        byte[] script,
        Timestamp expiration,
        string payload)
        : this(nexusName,
            chainName,
            script,
            expiration,
            Encoding.UTF8.GetBytes(payload))
    {
    }

    // transactions are always created unsigned, call Sign() to generate signatures
    public TransactionV2(
        string nexusName,
        string chainName,
        byte[] script,
        Timestamp expiration,
        byte[] payload = null)
    {
        Throw.IfNull(script, nameof(script));

        this.NexusName = nexusName;
        this.ChainName = chainName;
        this.Script = script;
        this.Expiration = expiration;
        this.Payload = payload != null ? payload :new byte[0];

        this.Signatures = new Signature[0];

        this.UpdateHash();
    }
    
    public TransactionV2(
        string nexusName,
        string chainName,
        byte[] script,
        Timestamp expiration,
        Address gasPayer, 
        Address gasTarget, 
        BigInteger gasLimit,
        BigInteger gasPrice,
        string payload)
        : this(nexusName,
            chainName,
            script,
            expiration,
            gasPayer,
            gasTarget,
            gasLimit,
            gasPrice,
            Encoding.UTF8.GetBytes(payload))
    {
    }
    
    public TransactionV2(
        string nexusName,
        string chainName,
        byte[] script,
        Timestamp expiration,
        Address gasPayer,
        Address gasTarget,
        BigInteger gasLimit,
        BigInteger gasPrice,
        byte[] payload = null)
    {
        Throw.IfNull(script, nameof(script));

        this.NexusName = nexusName;
        this.ChainName = chainName;
        this.Script = script;
        this.Expiration = expiration;
        this.Payload = payload != null ? payload :new byte[0];
        this.GasPayer = gasPayer;
        this.GasTarget = gasTarget;
        this.GasLimit = gasLimit;
        this.GasPrice = gasPrice;

        this.Signatures = new Signature[0];

        this.UpdateHash();
    }
    #endregion

    public override string ToString()
    {
        return Hash.ToString();
    }
    
    public byte[] ToByteArray(bool withSignature)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                Serialize(writer, withSignature);
            }

            return stream.ToArray();
        }
    }
    
    #region Signatures
    public void Sign(IKeyPair keypair, Func<byte[], byte[], byte[], byte[]> customSignFunction = null)
    {
        if (keypair == null)
        {
            throw new ChainException("Cannot sign with a null keypair");
        }

        var msg = this.ToByteArray(false);

        Signature sig = keypair.Sign(msg, customSignFunction);

        var sigs = new List<Signature>();

        if (this.Signatures != null && this.Signatures.Length > 0)
        {
            sigs.AddRange(this.Signatures);
        }

        sigs.Add(sig);
        this.Signatures = sigs.ToArray();
    }

    public void AddSignature(Signature signature)
    {
        var sigs = new List<Signature>();

        if (this.Signatures != null && this.Signatures.Length > 0)
        {
            sigs.AddRange(this.Signatures);
        }

        sigs.Add(signature);
        this.Signatures = sigs.ToArray();
    }

    public Signature GetTransactionSignature(IKeyPair keypair, Func<byte[], byte[], byte[], byte[]> customSignFunction = null)
    {
        if (keypair == null)
        {
            throw new ChainException("Cannot sign with a null keypair");
        }
        
        var msg = this.ToByteArray(false);

        Signature sig = keypair.Sign(msg, customSignFunction);

        return sig;
    }

    public bool IsSignedBy(Address address)
    {
        return IsSignedBy(new Address[] { address });
    }

    public bool IsSignedBy(IEnumerable<Address> addresses)
    {
        if (!HasSignatures)
        {
            return false;
        }

        var msg = this.ToByteArray(false);

        foreach (var signature in this.Signatures)
        {
            if (signature.Verify(msg, addresses))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateHash()
    {
        var data = this.ToByteArray(false);
        var hash = CryptoExtensions.Sha256(data);
        this.Hash = new Hash(hash);
    }
    #endregion
    
    #region Serialization
    public static TransactionV2? Unserialize(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        {
            using (var reader = new BinaryReader(stream))
            {
                return Unserialize(reader);
            }
        }
    }

    public static TransactionV2? Unserialize(BinaryReader reader)
    {
        var tx = new TransactionV2();
        try
        {
            tx.UnserializeData(reader);
            return tx;
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    public void Serialize(BinaryWriter writer, bool withSignature)
    {
        writer.WriteVarString(this.NexusName);
        writer.WriteVarString(this.ChainName);
        writer.WriteByteArray(this.Script);
        writer.Write(this.Expiration.Value);
        writer.WriteByteArray(this.Payload);
        writer.WriteAddress(this.GasPayer);
        writer.WriteAddress(this.GasTarget);
        writer.WriteBigInteger(this.GasPrice);
        writer.WriteBigInteger(this.GasLimit);
        
        if (withSignature)
        {
            writer.WriteVarInt(this.Signatures.Length);
            foreach (var signature in this.Signatures)
            {
                writer.WriteSignature(signature);
            }
        }
    }
    
    public void SerializeData(BinaryWriter writer)
    {
        this.Serialize(writer, true);
    }

    public void UnserializeData(BinaryReader reader)
    {
        this.NexusName = reader.ReadVarString();
        this.ChainName = reader.ReadVarString();
        this.Script = reader.ReadByteArray();
        this.Expiration = reader.ReadUInt32();
        this.Payload = reader.ReadByteArray();
        this.GasPayer = reader.ReadAddress();
        this.GasTarget = reader.ReadAddress();
        this.GasPrice = reader.ReadBigInteger();
        this.GasLimit = reader.ReadBigInteger();
        
        // check if we have some signatures attached
        try
        {
            var signatureCount = (int)reader.ReadVarInt();
            this.Signatures = new Signature[signatureCount];
            for (int i = 0; i < signatureCount; i++)
            {
                Signatures[i] = reader.ReadSignature();
            }
        }
        catch(Exception e)
        {
            Console.WriteLine($"Error:{e.Message} || {e.StackTrace}");
            this.Signatures = new Signature[0];
        }

        this.UpdateHash();
    }
    #endregion
    
    #region Mine
    public void Mine(ProofOfWork targetDifficulty)
    {
        Mine((int)targetDifficulty);
    }

    public void Mine(int targetDifficulty)
    {
        Throw.If(targetDifficulty < 0 || targetDifficulty > 256, "invalid difficulty");
        Throw.If(Signatures.Length > 0, "cannot be signed");

        if (targetDifficulty == 0)
        {
            return; // no mining necessary 
        }

        uint nonce = 0;

        while (true)
        {
            if (this.Hash.GetDifficulty() >= targetDifficulty)
            {
                return;
            }

            if (nonce == 0)
            {
                this.Payload = new byte[4];
            }

            nonce++;
            if (nonce == 0)
            {
                throw new ChainException("Transaction mining failed");
            }

            Payload[0] = (byte)((nonce >> 0) & 0xFF);
            Payload[1] = (byte)((nonce >> 8) & 0xFF);
            Payload[2] = (byte)((nonce >> 16) & 0xFF);
            Payload[3] = (byte)((nonce >> 24) & 0xFF);
            UpdateHash();
        }
    }
    #endregion
}
