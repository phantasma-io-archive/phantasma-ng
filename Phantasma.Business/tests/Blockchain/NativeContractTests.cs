using System.Reflection;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Storage.Context;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

public class NativeContractTests
{
    class MyNativeContract : NativeContract
    {
        public override NativeContractKind Kind { get; }
        
        private string _myField;

        public MyNativeContract() : base()
        {
            _myField = "default value";
        }
        
        public void MyMethod()
        {
            // implementation goes here
        }
    }
    
    /*[Theory]
    [InlineData("field1", "expected value 1")]
    [InlineData("field2", "expected value 2")]
    [InlineData("field3", "expected value 3")]
    public void TestLoadFromStorage(string fieldName, string expectedValue)
    {
        // arrange
        var storage = new MemoryStorageContext();
        var nativeContract = new MyNativeContract();

        // act
        nativeContract.LoadFromStorage(storage);

        // assert
        var contractType = nativeContract.GetType();
        var field = contractType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        var actualValue = field.GetValue(nativeContract);

        Assert.Equal(expectedValue, actualValue);
    }*/
    
    [Fact]
    public void TestNativeContractByName()
    {
        // arrange
        var swapContract = new SwapContract();
        
        // assert
        Assert.Equal(NativeContract.GetNativeContractByName("swap").Name, swapContract.Name);
    }
    
    [Fact]
    public void TestNativeContractByKind()
    {
        // arrange
        var swapContract = new SwapContract();
        
        // assert
        Assert.Equal(NativeContract.GetNativeContractByKind(NativeContractKind.Swap).Name, swapContract.Name);
        Assert.Null(NativeContract.GetNativeContractByKind(NativeContractKind.Unknown));
    }
    
    [Fact]
    public void TestHasInternalMethod()
    {
        // arrange
        var swapContract = new SwapContract();
        
        // assert
        Assert.True(swapContract.HasInternalMethod(nameof(SwapContract.SwapTokens)));
        Assert.False(swapContract.HasInternalMethod(nameof(GasContract.AllowGas)));
    }
    
}
