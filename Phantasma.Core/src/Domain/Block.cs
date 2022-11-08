using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain
{
    public sealed class Block : ISerializable
    {
        public Address ChainAddress { get; private set; }

        public BigInteger Height { get; private set; }
        public Timestamp Timestamp { get; private set; }
        public Hash PreviousHash { get; private set; }
        public uint Protocol { get; private set; }

        private bool _dirty;
        private Hash _hash;
        public Hash Hash
        {
            get
            {
                if (_dirty)
                {
                    UpdateHash();
                }

                return _hash;
            }
        }

        private List<Hash> _transactionHashes = new List<Hash>();
        public Hash[] TransactionHashes => _transactionHashes.ToArray();
        public int TransactionCount => _transactionHashes.Count;

        // stores the events for each included transaction
        private Dictionary<Hash, List<Event>> _transactionEvents = new Dictionary<Hash, List<Event>>();

        // stores the results of invocations
        private Dictionary<Hash, byte[]> _resultMap = new Dictionary<Hash, byte[]>();

        private Dictionary<Hash, ExecutionState> _stateMap = new Dictionary<Hash, ExecutionState>();

        // stores the results of oracles
        public List<OracleEntry> _oracleData = new List<OracleEntry>();
        public OracleEntry[] OracleData => _oracleData.Select(x => (OracleEntry)x).ToArray();

        public Address Validator { get; private set; }
        public Signature Signature { get; private set; }
        public byte[] Payload { get; private set; }

        public bool IsSigned => Signature != null;

        private List<Event> _blockEvents = new List<Event>();
        public IEnumerable<Event> Events => _blockEvents;

        // required for unserialization
        public Block()
        {
        }

        /// <summary>
        /// Note: When creating the genesis block of a new side chain, the previous block would be the block that contained the CreateChain call
        /// </summary>
        public Block(BigInteger height, Address chainAddress, Timestamp timestamp, Hash previousHash,
                uint protocol, Address validator, byte[] payload, IEnumerable<OracleEntry> oracleEntries = null)
        {
            this.ChainAddress = chainAddress;
            this.Timestamp = timestamp;
            this.Protocol = protocol;

            this.Height = height;
            this.PreviousHash = previousHash;

            this.Payload = payload;
            this.Validator = validator;
            this.Signature = null;

            this._oracleData = new List<OracleEntry>();
            if (oracleEntries != null)
            {
                foreach (var entry in oracleEntries)
                {
                    _oracleData.Add(entry);
                }
            }

            this._dirty = true;
        }

        public void AddAllTransactionHashes(IEnumerable<Hash> hashes)
        {
            _transactionHashes.AddRange(hashes);
            this._dirty = true;
        }

        public void AddTransactionHash(Hash hash)
        {
            _transactionHashes.Add(hash);
            this._dirty = true;
        }

        public void Sign(IKeyPair keys)
        {
            var msg = this.ToByteArray(false);
            this.Signature = keys.Sign(msg);
        }

        public void Notify(Event evt)
        {
            this._blockEvents.Add(evt);
        }

        public void Notify(Hash hash, Event evt)
        {
            List<Event> list;

            if (_transactionEvents.ContainsKey(hash))
            {
                list = _transactionEvents[hash];
            }
            else
            {
                list = new List<Event>();
                _transactionEvents[hash] = list;
            }

            list.Add(evt);
            _dirty = true;
        }

        internal void UpdateHash()
        {
            var data = ToByteArray(false);
            var hashBytes = CryptoExtensions.Sha256(data);
            _hash = new Hash(hashBytes);
            _dirty = false;
        }

        public Event[] GetEventsForTransaction(Hash hash)
        {
            if (_transactionEvents.ContainsKey(hash))
            {
                return _transactionEvents[hash].ToArray();
            }

            return new Event[0];
        }

        public byte[] GetResultForTransaction(Hash hash)
        {
            if (_resultMap.ContainsKey(hash))
            {
                return _resultMap[hash];
            }

            return null;
        }

        public ExecutionState GetStateForTransaction(Hash hash)
        {
            if (_stateMap.ContainsKey(hash))
            {
                return _stateMap[hash];
            }

            return ExecutionState.Fault;
        }


        #region SERIALIZATION

        public byte[] ToByteArray(bool withSignatures)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialize(writer, withSignatures);
                }

                return stream.ToArray();
            }
        }

        internal void Serialize(BinaryWriter writer, bool withSignatures)
        {
            writer.WriteBigInteger(Height);
            writer.Write(Timestamp.Value);
            writer.WriteHash(PreviousHash);
            writer.WriteAddress(ChainAddress);
            writer.WriteVarInt(Protocol);

            writer.WriteVarInt(_transactionHashes.Count);

            foreach (var hash in _transactionHashes)
            {
                writer.WriteHash(hash);
                var txEvents = GetEventsForTransaction(hash).ToArray();

                writer.WriteVarInt(txEvents.Length);
                foreach (var evt in txEvents)
                {
                    evt.Serialize(writer);
                }

                int resultLen = _resultMap.ContainsKey(hash) ? _resultMap[hash].Length : -1;
                writer.Write((short)resultLen);
                if (resultLen > 0)
                {
                    var result = _resultMap[hash];
                    writer.WriteByteArray(result);
                }
            }

            writer.WriteVarInt(_oracleData.Count);

            foreach (var entry in _oracleData)
            {
                writer.WriteVarString(entry.URL);
                writer.WriteByteArray(entry.Content);
            }

            if (Payload == null)
            {
                Payload = new byte[0];
            }

            writer.WriteVarInt(_blockEvents.Count);
            foreach (var evt in _blockEvents)
            {
                evt.Serialize(writer);
            }

            writer.WriteAddress(this.Validator);
            writer.WriteByteArray(this.Payload);

            if (Protocol >= DomainSettings.Phantasma30Protocol)
            {
                foreach (var hash in _transactionHashes)
                {
                    var state = _stateMap[hash];
                    writer.WriteVarInt((int)state);
                }
            }

            if (withSignatures)
            {
                writer.WriteSignature(this.Signature);
            }

            writer.Write((byte)0); // indicates the end of the block
        }

        public static Block Unserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return Unserialize(reader);
                }
            }
        }

        public static Block Unserialize(BinaryReader reader)
        {
            var block = new Block();
            block.UnserializeData(reader);
            return block;
        }

        public void SetResultForHash(Hash hash, byte[] result)
        {
            Throw.IfNull(result, nameof(result));
            Throw.If(result.Length > 32 * 1024, "transaction result is too large");
            _resultMap[hash] = result;
        }

        public void SetStateForHash(Hash hash, ExecutionState state)
        {
            _stateMap[hash] = state;
        }

        public void SerializeData(BinaryWriter writer)
        {
            Serialize(writer, true);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Height = reader.ReadBigInteger();
            this.Timestamp = new Timestamp(reader.ReadUInt32());
            this.PreviousHash = reader.ReadHash();
            this.ChainAddress = reader.ReadAddress();
            this.Protocol = (uint)reader.ReadVarInt();

            var hashCount = (uint)reader.ReadVarInt();
            _transactionHashes = new List<Hash>();

            _transactionEvents.Clear();
            _resultMap.Clear();
            _stateMap.Clear();
            for (int j = 0; j < hashCount; j++)
            {
                var hash = reader.ReadHash();
                _transactionHashes.Add(hash);

                var txEvtCount = (int)reader.ReadVarInt();
                var evts = new List<Event>(txEvtCount);
                for (int i = 0; i < txEvtCount; i++)
                {
                    evts.Add(Event.Unserialize(reader));
                }

                _transactionEvents[hash] = evts;

                var resultLen = reader.ReadInt16();
                if (resultLen >= 0)
                {
                    if (resultLen == 0)
                    {
                        _resultMap[hash] = new byte[0];
                    }
                    else
                    {
                        _resultMap[hash] = reader.ReadByteArray();
                    }
                }
            }

            var oracleCount = (uint)reader.ReadVarInt();
            _oracleData.Clear();
            while (oracleCount > 0)
            {
                var key = reader.ReadVarString();
                var val = reader.ReadByteArray();
                _oracleData.Add(new OracleEntry(key, val));
                oracleCount--;
            }

            var blockEvtCount = (int)reader.ReadVarInt();
            _blockEvents = new List<Event>(blockEvtCount);
            for (int i = 0; i < blockEvtCount; i++)
            {
                _blockEvents.Add(Event.Unserialize(reader));
            }

            Validator = reader.ReadAddress();

            Payload = reader.ReadByteArray();

            if (Protocol >= DomainSettings.Phantasma30Protocol)
            {
                foreach (var hash in _transactionHashes)
                {
                    _stateMap[hash] = (ExecutionState)reader.ReadVarInt();
                }
            }
            else
            {
                // before Phantasma 3.0 only txs with Halt state were added to blocks
                foreach (var hash in _transactionHashes)
                {
                    _stateMap[hash] = ExecutionState.Halt;
                }
            }

            try
            {
                Signature = reader.ReadSignature();

                var blockEnd = reader.ReadByte();
            }
            catch (Exception e)
            {
                Signature = null;
            }

            _dirty = true;
        }

        internal void CleanUp()
        {
            if (_transactionEvents.Count > 0)
            {
                _transactionEvents.Clear();
                _dirty = true;
            }

            if (_blockEvents.Count > 0)
            {
                _blockEvents.Clear();
                _dirty = true;
            }

            if (_oracleData.Count > 0)
            {
                _oracleData.Clear();
                _dirty = true;
            }
        }

        internal void MergeOracle(IOracleReader oracle)
        {
            if (oracle.Entries.Any())
            {
                _oracleData = oracle.Entries.ToList();
                _dirty = true;
            }
            oracle.Clear();
        }
        #endregion
    }
}
