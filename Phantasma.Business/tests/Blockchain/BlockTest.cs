using System.Collections.Generic;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Shared.Types;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain
{
    public class BlockTest
    {
        [Fact]
        public void null_block_test()
        {
            var block = new Block();
            block.Timestamp.ShouldBe(Timestamp.Null);
            block.Payload.ShouldBe(null);
            block.Events.ShouldBe(new List<Event>());
            block.OracleData.ShouldBe(new OracleEntry[0]);
            block.TransactionCount.ShouldBe(0);
            // TODO: Need to check transactions to be null
            //block.PreviousHash.ShouldBeNull<Hash>(Hash.Null);
            block.TransactionHashes.ShouldBe(new Hash[0]);
            block.Height.ShouldBe(0);
            block.ChainAddress.ShouldBe(Address.Null);
            block.Validator.ShouldBe(Address.Null);
        }
    }
}
