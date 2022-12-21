using System.Collections.Generic;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Hashing;
using Phantasma.Core.Domain;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class TransactionResultTests
{
    [Fact]
    public void TestTransactionResult_Empty()
    {
        var result = new TransactionResult();
        Assert.NotNull(result.Hash);
        Assert.Equal((uint)0, result.Code);
        Assert.Null(result.Result);
        Assert.Equal(ExecutionState.Running, result.State);
        Assert.Null(result.Log);
        Assert.Null(result.Info);
        Assert.Equal(0, result.Gas);
        Assert.Equal(0, result.GasUsed);
        Assert.Null(result.Events);
        Assert.Null(result.Codespace);
    }
    
    [Fact]
    public void TestTransactionResult_Full()
    {
        var hash = new Hash(CryptoExtensions.Sha256("tryandHashthis"));
        var code = (uint)1;
        var result = new VMObject();
        var state = ExecutionState.Fault;
        var log = "log";
        var info = "info";
        var gas = 100;
        var gasUsed = 50;
        var events = new List<Event>()
        {
            new Event(EventKind.Custom, Address.Null, "event1", null),
            new Event(EventKind.Custom, Address.Null, "event2", null),
        };
        var codespace = "codespace";
        
        var transactionResult = new TransactionResult(code, result, state, log, info, gas, gasUsed, events, codespace);
        transactionResult.Hash = hash;
        
        Assert.Equal(hash, transactionResult.Hash);
        Assert.Equal(code, transactionResult.Code);
        Assert.Equal(result, transactionResult.Result);
        Assert.Equal(state, transactionResult.State);
        Assert.Equal(log, transactionResult.Log);
        Assert.Equal(info, transactionResult.Info);
        Assert.Equal(gas, transactionResult.Gas);
        Assert.Equal(gasUsed, transactionResult.GasUsed);
        Assert.Equal(events, transactionResult.Events);
        Assert.Equal(codespace, transactionResult.Codespace);
    }
}
