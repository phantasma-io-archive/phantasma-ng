using Phantasma.Core.Cryptography;
using Shouldly;
using Xunit;
using Transaction = Phantasma.Core.Domain.Transaction;

namespace Phantasma.Business.Tests.Blockchain
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

    }
}
