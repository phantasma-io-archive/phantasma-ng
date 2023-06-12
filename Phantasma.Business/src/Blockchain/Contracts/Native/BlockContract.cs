using System;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Storage.Context;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class BlockContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Block;

        public BlockContract() : base()
        {
        }

        #region SETTLEMENTS
#pragma warning disable 0649
        internal StorageMap _settledTransactions; //<Hash, Hash>
        internal StorageMap _swapMap; // <Address, List<Hash>>
#pragma warning restore 0649

        [Obsolete("This method is not used any more",false)]
        public bool IsSettled(Hash hash)
        {
            return _settledTransactions.ContainsKey(hash);
        }

        private void RegisterHashAsKnown(Hash sourceHash, Hash targetHash)
        {
            _settledTransactions.Set(sourceHash, targetHash);
        }

        private void DoSettlement(IChain sourceChain, Address sourceAddress, Address targetAddress, string symbol, BigInteger value, byte[] data)
        {
            Runtime.Expect(Runtime.ProtocolVersion < 13, "this method is obsolete");
            Runtime.Expect(false, "this method is obsolete");
            Runtime.Expect(value > 0, "value must be greater than zero");
            Runtime.Expect(targetAddress.IsUser, "target must not user address");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var tokenInfo = Runtime.GetToken(symbol);


            /*if (tokenInfo.IsCapped())
            {
                var supplies = new SupplySheet(symbol, this.Runtime.Chain, Runtime.Nexus);
                
                if (IsAddressOfParentChain(sourceChain.Address))
                {
                    Runtime.Expect(supplies.MoveFromParent(this.Storage, value), "target supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(supplies.MoveFromChild(this.Storage, sourceChain.Name, value), "target supply check failed");
                }
            }
            */

            Runtime.SwapTokens(sourceChain.Name, sourceAddress, Runtime.Chain.Name, targetAddress, symbol, value);
        }

        [Obsolete("This method is not used anymore.", false)]
        public void SettleTransaction(Address sourceChainAddress, Hash hash)
        {
            Runtime.Expect(false, "block.SettleTransaction is obsolete");

            Runtime.Expect(Runtime.IsAddressOfParentChain(sourceChainAddress) || Runtime.IsAddressOfChildChain(sourceChainAddress), "source must be parent or child chain");

            Runtime.Expect(!IsSettled(hash), "hash already settled");

            var sourceChain = Runtime.GetChainByAddress(sourceChainAddress);

            var tx = Runtime.ReadTransactionFromOracle(DomainSettings.PlatformName, sourceChain.Name, hash);

            int settlements = 0;

            foreach (var transfer in tx.Transfers)
            {
                if (transfer.destinationChain == Runtime.Chain.Name)
                {
                    DoSettlement(sourceChain, transfer.sourceAddress, transfer.destinationAddress, transfer.Symbol, transfer.Value, transfer.Data);
                    settlements++;
                }
            }

            Runtime.Expect(settlements > 0, "no settlements in the transaction");
            RegisterHashAsKnown(hash, Runtime.Transaction.Hash);
        }
        #endregion
    }
}
