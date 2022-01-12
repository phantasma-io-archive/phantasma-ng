using Phantasma.Infrastructure;
using System;

namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("mempool list", Category = "Mempool", Description="List all transactions currently in mempool")]
        protected void OnMempoolListCommand(string[] args)
        {
            var mempool = NexusAPI.GetMempool();

            foreach (var tx in mempool.GetTransactions())
            {
                Console.WriteLine(tx.ToString());
            }
        }
    }
}
