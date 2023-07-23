using System.Collections.Generic;
using System.Linq;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class TransactionTests
{
    [Fact]
    public void NullTransactionTest()
    {
        var transaction = new Transaction();
        Assert.Equal(transaction.Expiration, Timestamp.Null);
        Assert.Equal(transaction.Payload, null);
        Assert.Equal(transaction.Payload, null);
        Assert.Equal(transaction.Script, null);
        Assert.Equal(transaction.Script, null);
        Assert.Equal(transaction.ToString(), "0000000000000000000000000000000000000000000000000000000000000000");
    }
    
    [Fact]
    public void CompareTransactionTest()
    {
        var transaction = new Transaction("test", "test", new byte[0]{}, Timestamp.Null, new byte[0]{});
        var transaction2 = new Transaction("test", "test", new byte[0]{}, Timestamp.Null, new byte[0]{});
        Assert.Equal(transaction, transaction2);
        Assert.Equal(transaction.Expiration, transaction2.Expiration);
        Assert.Equal(transaction.Payload, transaction2.Payload);
        Assert.Equal(transaction.Script, transaction2.Script);
        Assert.Equal(transaction.HasSignatures, transaction2.HasSignatures);
        Assert.Equal(transaction.Signatures, transaction2.Signatures);
        Assert.Equal(transaction.Hash, transaction2.Hash);
        
        Assert.Equal(transaction.ToString(), transaction2.ToString());
    }

    [Fact]
    public void CompareListTransactionTests()
    {
        var transactionList = new List<Transaction>();
        var transactionList2 = new List<Transaction>();
        for (int i = 0; i < 10; i++)
        {
            var transaction = new Transaction("test", "test", new byte[0] { }, Timestamp.Null, new byte[0] { });

            transactionList.Add(transaction);
            transactionList2.Add(transaction);
        }
        
        Assert.Equal(transactionList, transactionList2);
        Assert.Equal(transactionList.Count, transactionList2.Count);
        Assert.True(transactionList.Except(transactionList2).Count() == 0);
        
    }

}
