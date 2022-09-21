using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Shared.Types;
using System.Numerics;
using Phantasma.RpcClient;
using Phantasma.RpcClient.DTOs;
using RPCClient = Phantasma.RpcClient.Client.RpcClient;
using Shouldly;

namespace Phantasma.Integration.Tests;

[TestClass]
public class IntegrationTests
{
    private PhantasmaRpcService phantasmaService = new PhantasmaRpcService(new RPCClient(new Uri("http://localhost:5101/rpc"), httpClientHandler: new HttpClientHandler { }));

    [TestMethod]
    public void A_failed_tx_test()
    {
        var owner = PhantasmaKeys.FromWIF("KxMn2TgXukYaNXx7tEdjh7qB2YaMgeuKy47j4rvKigHhBuZWeP3r");
        var user = PhantasmaKeys.Generate();
        var ownerBalanceBefore = GetBalance(owner.Address.ToString(), "SOUL");

        var sb = ScriptUtils.BeginScript()
            .AllowGas(owner.Address, Address.Null)
            .TransferTokens("SOUL", owner.Address, user.Address, BigInteger.Parse("20000000000000000"));
        var script = sb.EndScript();
        var tx = new Transaction("", DomainSettings.RootChainName, script, owner.Address, owner.Address, Address.Null, 100000, 9999, Timestamp.Now + TimeSpan.FromDays(300));
        tx.Mine(ProofOfWork.Minimal);
        tx.Sign(owner);

        var txString = Base16.Encode(tx.ToByteArray(true));
        var txHash = phantasmaService.SendRawTx.SendRequestAsync(txString, "1").GetAwaiter().GetResult();

        Thread.Sleep(2000);
        var txResult = phantasmaService.GetTxByHash.SendRequestAsync(txHash, "1").GetAwaiter().GetResult();
        txResult.State.ShouldBe("Fault");

        var balance = GetBalance(user.Address.ToString(), "SOUL");
        // empty address no balance yet
        balance.Valid.ShouldBeFalse();

        var ownerBalanceAfter = GetBalance(owner.Address.ToString(), "SOUL");

        var evt = GetEvents(EventKind.Error, txResult);
        var evtContent = evt.Event.GetContent<string>();
        evtContent.ShouldBe("SOUL balance subtract failed from " + owner.Address.ToString() + " @ TransferTokens");

        ownerBalanceBefore.Valid.ShouldBe(true);
        ownerBalanceAfter.Valid.ShouldBe(true);
        ownerBalanceAfter.Amount.ShouldBe(ownerBalanceBefore.Amount);
    }

    [TestMethod]
    public void B_mint_test()
    {
        var owner = PhantasmaKeys.FromWIF("KxMn2TgXukYaNXx7tEdjh7qB2YaMgeuKy47j4rvKigHhBuZWeP3r");
        var user = PhantasmaKeys.Generate();
        var ownerBalanceBefore = GetBalance(owner.Address.ToString(), "SOUL");

        var sb = ScriptUtils.BeginScript()
            .AllowGas(owner.Address, Address.Null)
            .CallInterop("Runtime.MintTokens", "S3dP2jjf1jUG9nethZBWbnu9a6dFqB7KveTWU7znis6jpDy", owner.Address, "SOUL", 1000000)
            .SpendGas(owner.Address);
        var script = sb.EndScript();
        var tx = new Transaction("", DomainSettings.RootChainName, script, owner.Address, owner.Address, Address.Null, 100000, 9999, Timestamp.Now + TimeSpan.FromDays(300));
        tx.Mine(ProofOfWork.Minimal);
        tx.Sign(owner);

        var txString = Base16.Encode(tx.ToByteArray(true));
        var txHash = phantasmaService.SendRawTx.SendRequestAsync(txString, "1").GetAwaiter().GetResult();

        Thread.Sleep(2000);
        var txResult = phantasmaService.GetTxByHash.SendRequestAsync(txHash, "1").GetAwaiter().GetResult();
        txResult.State.ShouldBe("Fault");

        var balance = GetBalance(user.Address.ToString(), "SOUL");
        // empty address no balance yet
        balance.Valid.ShouldBeFalse();

        var ownerBalanceAfter = GetBalance(owner.Address.ToString(), "SOUL");

        var evt = GetEvents(EventKind.Error, txResult);
        var evtContent = evt.Event.GetContent<string>();
        evtContent.ShouldBe("SOUL balance subtract failed from " + owner.Address.ToString() + " @ TransferTokens");

        ownerBalanceBefore.Valid.ShouldBe(true);
        ownerBalanceAfter.Valid.ShouldBe(true);
        ownerBalanceAfter.Amount.ShouldBe(ownerBalanceBefore.Amount);
    }

    private (Event Event, bool Valid) GetEvents(EventKind kind, TransactionDto txDto)
    {
        foreach (var evt in txDto.Events)
        {
            if (evt.EventKind.ToString() == kind.ToString())
            {
                return (new Event(evt.EventKind, Address.FromText(evt.EventAddress), evt.Contract, Base16.Decode(evt.Data)), true);
            }
        }

        return (default(Event), false);
    }

    private (BigInteger Amount, bool Valid) GetBalance(string address, string token)
    {
        var account = phantasmaService.GetAccount.SendRequestAsync(address, "1").GetAwaiter().GetResult();

        foreach (var balance in account.Tokens)
        {
            Console.WriteLine("symbol: " + balance.Symbol);
            if (balance.Symbol == token)
            {
                Console.WriteLine("found: " + balance.Symbol);
                return (BigInteger.Parse(balance.Amount), true);
            }
        }

        return (new BigInteger(0), false);
    }
}
