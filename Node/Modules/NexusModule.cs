using System;
using System.Linq;
using System.Numerics;

using Phantasma.Core;
using Phantasma.Business;
using Phantasma.Spook.Command;
using Serilog.Core;

namespace Phantasma.Spook.Modules
{
    [Module("nexus")]
    public static class NexusModule
    {
        public static Logger logger => Spook.Logger;

        public static void Rescan(Nexus oldNexus, PhantasmaKeys owner, string[] args)
        {
            /*if (args.Length != 1)
            {
                throw new CommandException("Expected args: file_path");
            }*/

            var genesisAddress = oldNexus.GetGenesisAddress(oldNexus.RootStorage);
            if (owner.Address != genesisAddress)
            {
                throw new CommandException("Invalid owner key");
            }

            var oldGenesisBlock = oldNexus.GetGenesisBlock();

            var newNexus = new Nexus(oldNexus.Name);
            newNexus.CreateGenesisBlock(owner, oldGenesisBlock.Timestamp, DomainSettings.LatestKnownProtocol);

            var oldRootChain = oldNexus.RootChain;
            var newRootChain = newNexus.RootChain;

            var height = oldRootChain.Height;
            BigInteger minFee = 0;
            Hash previousHash = Hash.Null;
            for (int i=1; i<=height; i++)
            {
                logger.Information($"Processing block {i} out of {height}");
                var oldBlockHash = oldRootChain.GetBlockHashAtHeight(i);
                var oldBlock = oldRootChain.GetBlockByHash(oldBlockHash);

                var transactions = oldBlock.TransactionHashes.Select(x => oldRootChain.GetTransactionByHash(x));

                try
                {
                    var newBlock = new Block(oldBlock.Height, oldBlock.ChainAddress, oldBlock.Timestamp, oldBlock.TransactionHashes, previousHash, oldBlock.Protocol, owner.Address, oldBlock.Payload);
		            Transaction inflationTx = null;
                    var changeSet = newRootChain.ProcessBlock(newBlock, transactions, minFee, out inflationTx, null);
		            if (inflationTx != null)
		            {
			            transactions = transactions.Concat(new [] { inflationTx });
		            }
                    newBlock.Sign(owner);
                    newRootChain.AddBlock(newBlock, transactions, minFee, changeSet);
                }
                catch (Exception e)
                {
                    throw new CommandException("Block validation failed: "+e.Message);
                }
            }

        }
    }
}
