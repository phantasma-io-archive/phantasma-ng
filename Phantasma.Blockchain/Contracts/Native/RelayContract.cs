﻿using System.IO;
using System.Text;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct RelayMessage : ISerializable
    {
        public string nexus;
        public BigInteger index;
        public Timestamp timestamp;
        public Address sender;
        public Address receiver;
        public byte[] script;

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

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(nexus);
            writer.WriteBigInteger(index);
            writer.Write(timestamp.Value);
            writer.WriteAddress(sender);
            writer.WriteAddress(receiver);
            writer.WriteByteArray(script);
        }

        public void UnserializeData(BinaryReader reader)
        {
            nexus = reader.ReadVarString();
            index = reader.ReadBigInteger();
            timestamp = new Timestamp(reader.ReadUInt32());
            sender = reader.ReadAddress();
            receiver = reader.ReadAddress();
            script = reader.ReadByteArray();
        }
    }

    public struct RelayReceipt : ISerializable
    {
        public RelayMessage message;
        public Signature signature;

        public void SerializeData(BinaryWriter writer)
        {
            message.SerializeData(writer);
            writer.WriteSignature(signature);
        }

        public void UnserializeData(BinaryReader reader)
        {
            message.UnserializeData(reader);
            signature = reader.ReadSignature();
        }

        public static RelayReceipt FromBytes(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var receipt = new RelayReceipt();
                    receipt.UnserializeData(reader);
                    return receipt;
                }
            }
        }

        public static RelayReceipt FromMessage(RelayMessage msg, KeyPair keys)
        {
            var bytes = msg.ToByteArray();
            var signature = keys.Sign(bytes);
            return new RelayReceipt()
            {
                message = msg,
                signature = signature
            };
        }
    }

    public sealed class RelayContract : SmartContract
    {
        public override string Name => "relay";

        public static readonly int MinimumReceiptsPerTransaction = 20;

        public static readonly BigInteger RelayFeePerMessage = 10;

        internal StorageMap _balances; //<address, BigiInteger>
        internal StorageMap _indices; //<string, BigiInteger>

        public RelayContract() : base()
        {
        }

        private string MakeKey(Address sender, Address receiver)
        {
            return sender.Text + ">" + receiver.Text;
        }

        public BigInteger GetBalance(Address from)
        {
            if (_balances.ContainsKey<Address>(from))
            {
                return _balances.Get<Address, BigInteger>(from);
            }
            return 0;
        }

        public BigInteger GetIndex(Address from, Address to)
        {
            var key = MakeKey(from, to);
            if (_indices.ContainsKey<string>(key))
            {
                return _indices.Get<string, BigInteger>(key);
            }

            return 0;
        }

        public Address GetTopUpAddress(Address from)
        {
            var bytes = Encoding.UTF8.GetBytes(from.Text+".relay");
            var hash = CryptoExtensions.SHA256(bytes);
            return new Address(hash);
        }

        /*
        public void OpenChannel(Address from, Address to, string chainName, string channelName, string tokenSymbol, BigInteger amount, BigInteger fee)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(from != to, "invalid target");

            Runtime.Expect(amount > 0, "invalid amount");

            Runtime.Expect(fee > 0 && fee<amount, "invalid fee");

            Runtime.Expect(Runtime.Nexus.ChainExists(chainName), "invalid chain");

            Runtime.Expect(Runtime.Nexus.TokenExists(tokenSymbol), "invalid base token");
            var token = Runtime.Nexus.GetTokenInfo(tokenSymbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var key = MakeKey(from, channelName);
            Runtime.Expect(!_channelMap.ContainsKey<string>(key), "channel already open");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, tokenSymbol, from, Runtime.Chain.Address, amount), "insuficient balance");

            var channel = new RelayChannel()
            {
                balance = amount,
                fee = fee,
                owner = to,
                chain = chainName,
                creationTime = Runtime.Time,
                symbol = tokenSymbol,
                active = true,
                index = 0,
            };
            _channelMap.Set<string, RelayChannel>(key, channel);

            var list = _channelList.Get<Address, StorageList>(from);
            list.Add<string>(channelName);

            var address = GetAddress(from, chainName);
            // TODO create auto address 


            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = channel.symbol });
            Runtime.Notify(EventKind.ChannelOpen, from, channelName);
        }

        public void CloseChannel(Address from, string channelName)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var key = MakeKey(from, channelName);
            Runtime.Expect(_channelMap.ContainsKey<string>(key), "invalid channel");

            var channel = _channelMap.Get<string, RelayChannel>(key);
            Runtime.Expect(channel.active, "channel already closed");

            channel.active = false;
            _channelMap.Set<string, RelayChannel>(key, channel);
            Runtime.Notify(EventKind.ChannelClose, from, channelName);
        }*/

        public void TopUpChannel(Address from, BigInteger amount)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            BigInteger balance = _balances.ContainsKey(from) ? _balances.Get<Address, BigInteger>(from) : 0;

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, from, Runtime.Chain.Address, amount), "insuficient balance");
            balance += amount;
            Runtime.Expect(balance >= 0, "invalid balance");
            _balances.Set<Address, BigInteger>(from, balance);

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = channel.symbol });
        }

        public void UpdateChannel(RelayReceipt receipt)
        {
            var channelIndex = GetIndex(receipt.message.sender, receipt.message.receiver);
            // here we count how many receipts we are implicitly accepting
            // this means that we don't need to accept every receipt, allowing skipping several
            var receiptCount = 1 + receipt.message.index - channelIndex;
            Runtime.Expect(receiptCount > 0, "invalid receipt index");

            var payout = RelayFeePerMessage * receiptCount;

            var balance = GetBalance(receipt.message.sender);
            Runtime.Expect(balance >= payout, "insuficient balance");

            var bytes = receipt.message.ToByteArray();
            Runtime.Expect(receipt.signature.Verify(bytes, receipt.message.sender), "invalid signature");

            balance -= payout;
            _balances.Set<Address, BigInteger>(receipt.message.sender, balance);
            var key = MakeKey(receipt.message.sender, receipt.message.receiver);
            _indices.Set<string, BigInteger>(key, receipt.message.index);

            Runtime.Expect(payout > 0, "invalid payout");

            // TODO proper define a payment address here?
            Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, Runtime.Chain.Address, Runtime.Chain.Address, payout);
            Runtime.Notify(EventKind.TokenReceive, Runtime.Chain.Address, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = payout, symbol = channel.symbol });
        }
    }
}
