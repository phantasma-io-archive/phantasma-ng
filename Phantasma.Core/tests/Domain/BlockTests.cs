using System.IO;
using System.Linq;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class BlockTests
{
    [Fact]
    public void TestAddTransaction()
    {
        var block = new Block();
        block.AddTransactionHash(Hash.Null);
        Assert.Equal(1, block.TransactionCount);
    }

    [Fact]
    public void TestIsSigned()
    {
        var block = new Block();
        Assert.False(block.IsSigned);
    }

    [Fact]
    public void TestEvents()
    {
        var block = new Block();
        block.Notify(new Event());
        Assert.Equal(1, block.Events.Count());
    }

    [Fact]
    public void TestGetStateForTransaction()
    {
        var block = new Block();
        var result = block.GetStateForTransaction(Hash.Null);  
        Assert.Equal(ExecutionState.Fault, result);
    }

    [Fact]
    public void TestSetStateForHash()
    {
        var block = new Block();
        Assert.Throws<ChainException>(() => block.SetStateForHash(Hash.Null, ExecutionState.Running));
    }

    [Fact]
    public void TestSerializeData()
    {
        var block = new Block(1, Address.Null, Timestamp.Now, Hash.Null, DomainSettings.LatestKnownProtocol, Address.Null, Encoding.UTF8.GetBytes("Test"));
        //(BigInteger height, Address chainAddress, Timestamp timestamp, Hash previousHash,uint protocol, Address validator, byte[] payload, IEnumerable<OracleEntry> oracleEntries = null)
        var bytes = new byte[2048];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        block.SerializeData(writer);
        stream.Position = 0;
        var reader = new BinaryReader(stream);
        var block2 = new Block();
        block2.UnserializeData(reader);
        
        Assert.Equal(block.Timestamp, block2.Timestamp);

    }
}
