using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain
{
    public struct TransactionGas : IComparable<TransactionGas>, IEquatable<TransactionGas>
    {
        public static readonly TransactionGas Null = new TransactionGas();
        public Address GasPayer;
        public Address GasTarget;
        public BigInteger GasLimit;
        public BigInteger GasPrice;
        
        #region Operations
        public int CompareTo(TransactionGas other)
        {
            return this.GasPayer == other.GasPayer && this.GasTarget == other.GasTarget && 
                this.GasLimit == other.GasLimit && this.GasPrice == other.GasPrice ? 1 : 0;
        }

        bool IEquatable<TransactionGas>.Equals(TransactionGas other)
        {
            return Equals(other);
        }

        public static bool operator ==(TransactionGas left, TransactionGas right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TransactionGas left, TransactionGas right)
        {
            return !(left == right);
        }


        public static bool operator >(TransactionGas left, TransactionGas right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(TransactionGas left, TransactionGas right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator <(TransactionGas left, TransactionGas right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(TransactionGas left, TransactionGas right)
        {
            return left.CompareTo(right) <= 0;
        }
        #endregion
    }
    
    public sealed class Transaction : ITransaction, ISerializable
    {
        public readonly static Transaction Null = null;

        public static readonly int DefaultGasLimit = 9999;

        public byte[] Script { get; private set; }

        public string NexusName { get; private set; }

        public string ChainName { get; private set; }
        
        public Timestamp Expiration { get; private set; }

        public byte[] Payload { get; private set; }

        public Signature[] Signatures { get; private set; }

        public Hash Hash { get; private set; }
        
        //public TransactionGas TransactionGas { get; private set; }

        public static Transaction? Unserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return Unserialize(reader);
                }
            }
        }

        public static Transaction? Unserialize(BinaryReader reader)
        {
            var tx = new Transaction();
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

            /*if (TransactionGas.Null.GasPayer != TransactionGas.GasPayer  &&
                TransactionGas.Null.GasLimit != TransactionGas.GasLimit && TransactionGas.Null.GasPrice != TransactionGas.GasPrice)
            {
                writer.WriteAddress(this.TransactionGas.GasPayer);
                writer.WriteAddress(this.TransactionGas.GasTarget);
                writer.WriteBigInteger(this.TransactionGas.GasLimit);
                writer.WriteBigInteger(this.TransactionGas.GasPrice);
            }*/

            if (withSignature)
            {
                writer.WriteVarInt(this.Signatures.Length);
                foreach (var signature in this.Signatures)
                {
                    writer.WriteSignature(signature);
                }
            }
        }

        public override string ToString()
        {
            return Hash.ToString();
        }

        // required for deserialization
        public Transaction()
        {
            this.Hash = Hash.Null;
            //this.TransactionGas = TransactionGas.Null;
        }

        public Transaction(
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
        
        public Transaction(
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

        // transactions are always created unsigned, call Sign() to generate signatures
        public Transaction(
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
            //this.TransactionGas = TransactionGas.Null;

            this.Signatures = new Signature[0];

            this.UpdateHash();
        }
        
        public Transaction(
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
            /*this.TransactionGas = new TransactionGas
            {
                GasPayer = gasPayer,
                GasTarget = gasTarget,
                GasLimit = gasLimit,
                GasPrice = gasPrice
            };*/
            
            this.Payload = payload != null ? payload :new byte[0];

            this.Signatures = new Signature[0];

            this.UpdateHash();
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

        public bool HasSignatures => Signatures != null && Signatures.Length > 0;

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
            var pointer = reader.BaseStream.Position;

            /*try
            {
                var gasPayer = reader.ReadAddress();
                var gasTarget = reader.ReadAddress();
                var gasLimit = reader.ReadBigInteger();
                var gasPrice = reader.ReadBigInteger();
                if ( !Address.IsValidAddress(gasPayer.Text) || !Address.IsValidAddress(gasTarget.Text) )
                {
                    throw new Exception("Invalid address");
                }
                this.TransactionGas = new TransactionGas
                {
                    GasPayer = gasPayer,
                    GasTarget = gasTarget,
                    GasLimit = gasLimit,
                    GasPrice = gasPrice
                };
            }
            catch(Exception e)
            {
                Console.WriteLine($"Error:{e.Message} || {e.StackTrace}");
                this.TransactionGas = TransactionGas.Null;
                reader.BaseStream.Position = pointer;
            }*/

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

        public override bool Equals(object obj)
        {
            if ( obj is not Transaction)
            {
                return false;
            }
            else if (obj == null)
            {
                return false;
            }
            else if (obj == this)
            {
                return true;
            }
            else if (obj is Transaction tx)
            {
                bool result = this.Hash == ((Transaction)obj).Hash;

                return this.Hash.Equals(tx.Hash) && this.Payload.SequenceEqual(tx.Payload) && this.Script.SequenceEqual(tx.Script)
                       && this.Expiration.Equals(tx.Expiration) && this.ChainName == tx.ChainName &&
                       this.NexusName == tx.NexusName && this.Signatures.Length == tx.Signatures.Length 
                    /*&& this.Signatures.Except(tx.Signatures).Count() == 0 && tx.Signatures.Except(this.Signatures).Count() == 0*/;
            }
            
            return base.Equals(obj);
        }
    }
}
