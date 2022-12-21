using System.Text;
using Phantasma.Core.Domain;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class SmartContractTests
{
    [Fact]
    public void TestGetAddressForNative()
    {
        var address = SmartContract.GetAddressForNative(NativeContractKind.Account);
        Assert.Equal("S3dGz1deZweAiMVPHL328X3pVNpANQVjgX4MoRGpbNNAfrB", address.ToString());
    }

    [Fact]
    public void TestGetAddressFromContractName()
    {
        var address = SmartContract.GetAddressFromContractName("KCAL");
        Assert.Equal("S3dP6LRC3f3xw4ZZ2HH9BQHzYNHuHS8vetbCQpkMFvRmVEF", address.ToString());
    }

    [Fact]
    public void TestGetKeyForField()
    {
        var key = SmartContract.GetKeyForField("MyContract", "myField", false);
        var expectedKey = Encoding.UTF8.GetBytes("MyContract.myField");
        Assert.Equal(expectedKey, key);
    }

    [Fact]
    public void TestGetProtectedKeyForField()
    {
        var key = SmartContract.GetKeyForField("MyContract", "myField", true);
        var expectedKey = Encoding.UTF8.GetBytes(".MyContract.myField");
        Assert.Equal(expectedKey, key);
    }
    
    [Fact]
    public void TestToString()
    {
        var address = new SmartContractImplementationTests("KCAL");
        Assert.Equal("KCAL", address.ToString());
    }
    
    public class SmartContractImplementationTests : SmartContract
    {
        public SmartContractImplementationTests(string name)
        {
            Name = name;
        }

        public override string Name { get; }
    }
}
