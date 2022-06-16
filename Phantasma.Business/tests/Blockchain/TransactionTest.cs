using Phantasma.Core;
using Shouldly;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using Xunit;
using static Phantasma.Core.WalletLink;

namespace Phantasma.Business.Tests
{
    public class TransactionTest
    {
        [Fact]
        public void null_transaction_test()
        {
            var transaction = new Transaction();
            transaction.NexusName.ShouldBe(null);
            transaction.ChainName.ShouldBe(null);
            transaction.Hash.ShouldBeOfType<Hash>();
            transaction.HasSignatures.ShouldBe(false);
            transaction.Signatures.ShouldBe(null);
            transaction.Script.ShouldBe(null);
            transaction.Payload.ShouldBe(null);
        }

        [Fact]
        public void is_signed_by()
        {
            
        }
    }
}
