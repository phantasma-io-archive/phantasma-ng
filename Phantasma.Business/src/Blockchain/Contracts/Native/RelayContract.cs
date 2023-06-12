using System.Numerics;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Relay;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class RelayContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Relay;

        public static readonly int MinimumReceiptsPerTransaction = 20;

        public static readonly BigInteger RelayFeePerMessage = UnitConversion.GetUnitValue(DomainSettings.FuelTokenDecimals) / (1000 * StakeContract.DefaultEnergyRatioDivisor);

#pragma warning disable 0649
        internal StorageMap _keys; //<address, ECPoint>
        internal StorageMap _balances; //<address, BigInteger>
        internal StorageMap _indices; //<string, BigInteger>
#pragma warning restore 0649

        public RelayContract() : base()
        {
        }

        /// <summary>
        /// Create a new relay address for the given chain name
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="receiver"></param>
        /// <returns></returns>
        private string MakeKey(Address sender, Address receiver)
        {
            return sender.Text + ">" + receiver.Text;
        }

        /// <summary>
        /// Returns the balance of the given address
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public BigInteger GetBalance(Address from)
        {
            if (_balances.ContainsKey(from))
            {
                return _balances.Get<Address, BigInteger>(from);
            }
            return 0;
        }

        /// <summary>
        /// Get the index of the given address pair
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public BigInteger GetIndex(Address from, Address to)
        {
            var key = MakeKey(from, to);
            if (_indices.ContainsKey(key))
            {
                return _indices.Get<string, BigInteger>(key);
            }

            return 0;
        }

        
        /// <summary>
        /// Get the topup address for the given address
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public Address GetTopUpAddress(Address from)
        {
            var bytes = Encoding.UTF8.GetBytes(from.Text + ".relay");
            return Address.FromHash(bytes);
        }

        /*
        public void OpenChannel(Address from, Address to, string chainName, string channelName, string tokenSymbol, BigInteger amount, BigInteger fee)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from != to, "invalid target");

            Runtime.Expect(amount > 0, "invalid amount");

            Runtime.Expect(fee > 0 && fee<amount, "invalid fee");

            Runtime.Expect(Runtime.Nexus.ChainExists(chainName), "invalid chain");

            Runtime.Expect(Runtime.Nexus.TokenExists(tokenSymbol), "invalid base token");
            var token = Runtime.Nexus.GetTokenInfo(tokenSymbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var key = MakeKey(from, channelName);
            Runtime.Expect(!_channelMap.ContainsKey<string>(key), "channel already open");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, tokenSymbol, from, this.Address, amount), "insuficient balance");

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


            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Address, value = amount, symbol = channel.symbol });
            Runtime.Notify(EventKind.ChannelOpen, from, channelName);
        }

        public void CloseChannel(Address from, string channelName)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var key = MakeKey(from, channelName);
            Runtime.Expect(_channelMap.ContainsKey<string>(key), "invalid channel");

            var channel = _channelMap.Get<string, RelayChannel>(key);
            Runtime.Expect(channel.active, "channel already closed");

            channel.active = false;
            _channelMap.Set<string, RelayChannel>(key, channel);
            Runtime.Notify(EventKind.ChannelClose, from, channelName);
        }*/

        /// <summary>
        /// Opens a new channel for the given address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="publicKey"></param>
        public void OpenChannel(Address from, byte[] publicKey)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(!_keys.ContainsKey(from), "channel already open");

            _keys.Set(from, publicKey);

            Runtime.Notify(EventKind.ChannelCreate, from, publicKey);
        }

        /// <summary>
        /// Returns the public key of the channel
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public byte[] GetKey(Address from)
        {
            Runtime.Expect(_keys.ContainsKey(from), "channel not open");
            return _keys.Get<Address, byte[]>(from);
        }
        
        /// <summary>
        /// Topup the given channel with the given amount
        /// </summary>
        /// <param name="from"></param>
        /// <param name="count"></param>
        public void TopUpChannel(Address from, BigInteger count)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(count >= 1, "insufficient topup amount");
            var amount = RelayFeePerMessage * count;

            Runtime.Expect(_keys.ContainsKey(from), "channel not open");

            BigInteger balance = _balances.ContainsKey(from) ? _balances.Get<Address, BigInteger>(from) : 0;

            var availableBalance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, from);
            Runtime.Expect(availableBalance >= amount, $"insufficient balance in account {availableBalance}/{amount}");
            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, Address, amount);
            balance += amount;
            Runtime.Expect(balance >= 0, "invalid balance");
            _balances.Set(from, balance);

            Runtime.Notify(EventKind.ChannelRefill, from, count);
        }

        /// <summary>
        /// Create a new relay message
        /// </summary>
        /// <param name="receipt"></param>
        public void SettleChannel(RelayReceipt receipt)
        {
            var channelIndex = GetIndex(receipt.message.sender, receipt.message.receiver);
            // check for possible replay attack
            Runtime.Expect(receipt.message.nexus == Runtime.NexusName, "invalid nexus name");

            // here we count how many receipts we are implicitly accepting
            // this means that we don't need to accept every receipt, allowing skipping several
            var receiptCount = 1 + receipt.message.index - channelIndex;
            Runtime.Expect(receiptCount > 0, "invalid receipt index");

            var expectedFee = RelayFeePerMessage * receiptCount;

            var balance = GetBalance(receipt.message.sender);
            Runtime.Expect(balance >= expectedFee, "insuficient balance");

            var bytes = receipt.message.ToByteArray();
            Runtime.Expect(receipt.signature.Verify(bytes, receipt.message.sender), "invalid signature");

            balance -= expectedFee;
            _balances.Set(receipt.message.sender, balance);
            var key = MakeKey(receipt.message.sender, receipt.message.receiver);
            _indices.Set(key, receipt.message.index + 1);

            Runtime.Expect(expectedFee > 0, "invalid payout");

            var payout = expectedFee / 2;

            // send half to the chain
            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, Address, Address, payout);

            // send half to the receiver
            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, Address, receipt.message.receiver, payout);

            Runtime.Notify(EventKind.ChannelSettle, receipt.message.sender, receiptCount);
        }
    }
}
