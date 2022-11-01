using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using System.Numerics;
using Phantasma.RpcClient;
using Phantasma.RpcClient.DTOs;
using RPCClient = Phantasma.RpcClient.Client.RpcClient;
using Shouldly;
using Docker.DotNet;
using Xunit;
using Docker.DotNet.Models;

namespace Phantasma.Integration.Tests;

public class IntegrationTests : IDisposable
{
    private PhantasmaRpcService PhantasmaService { get; set; }
    private DockerClient DockerClient { get; set; }
    private string CurrentContainerId { get; set; } = null; 

    [Fact]
    [Trait("Category", "Integration")]
    public void failed_tx_test()
    {
        var owner = PhantasmaKeys.FromWIF("KxMn2TgXukYaNXx7tEdjh7qB2YaMgeuKy47j4rvKigHhBuZWeP3r");
        var user = PhantasmaKeys.Generate();
        var ownerBalanceBefore = GetBalance(owner.Address.ToString(), "SOUL");

        var sb = ScriptUtils.BeginScript()
            .AllowGas()
            .TransferTokens("SOUL", owner.Address, user.Address, BigInteger.Parse("20000000000000000"));
        var script = sb.EndScript();
        var tx = new Transaction(
            "simnet",
            DomainSettings.RootChainName,
            script,
            owner.Address,
            owner.Address,
            100000,
            999,
            Timestamp.Now + TimeSpan.FromDays(300),
            "IntegrationTest");
        tx.Mine(ProofOfWork.Minimal);
        tx.Sign(owner);

        var txString = Base16.Encode(tx.ToByteArray(true));
        var txHash = PhantasmaService.SendRawTx.SendRequestAsync(txString, "1").GetAwaiter().GetResult();

        Thread.Sleep(2000);
        var txResult = PhantasmaService.GetTxByHash.SendRequestAsync(txHash, "1").GetAwaiter().GetResult();
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

    [Fact]
    [Trait("Category", "Integration")]
    public void mint_soul_test()
    {
        var owner = PhantasmaKeys.FromWIF("KxMn2TgXukYaNXx7tEdjh7qB2YaMgeuKy47j4rvKigHhBuZWeP3r");
        var user = PhantasmaKeys.Generate();
        var ownerBalanceBefore = GetBalance(owner.Address.ToString(), "SOUL");

        var sb = ScriptUtils.BeginScript()
            .AllowGas()
            .CallInterop("Runtime.MintTokens", "S3dP2jjf1jUG9nethZBWbnu9a6dFqB7KveTWU7znis6jpDy", owner.Address, "SOUL", 1000000)
            .SpendGas();
        var script = sb.EndScript();
        var tx = new Transaction(
            "simnet",
            DomainSettings.RootChainName,
            script,
            owner.Address,
            owner.Address,
            100000,
            999,
            Timestamp.Now + TimeSpan.FromDays(300),
            "IntegrationTest");
        tx.Mine(ProofOfWork.Minimal);
        tx.Sign(owner);

        var txString = Base16.Encode(tx.ToByteArray(true));
        var txHash = PhantasmaService.SendRawTx.SendRequestAsync(txString, "1").GetAwaiter().GetResult();

        Thread.Sleep(2000);
        var txResult = PhantasmaService.GetTxByHash.SendRequestAsync(txHash, "1").GetAwaiter().GetResult();
        txResult.State.ShouldBe("Fault");

        var balance = GetBalance(user.Address.ToString(), "SOUL");
        // empty address no balance yet
        balance.Valid.ShouldBeFalse();

        var ownerBalanceAfter = GetBalance(owner.Address.ToString(), "SOUL");

        var evt = GetEvents(EventKind.Error, txResult);
        var evtContent = evt.Event.GetContent<string>();
        evtContent.ShouldBe("Minting system token SOUL not allowed");

        ownerBalanceBefore.Valid.ShouldBe(true);
        ownerBalanceAfter.Valid.ShouldBe(true);
        ownerBalanceAfter.Amount.ShouldBe(ownerBalanceBefore.Amount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void mint_kcal_test()
    {
        var owner = PhantasmaKeys.FromWIF("KxMn2TgXukYaNXx7tEdjh7qB2YaMgeuKy47j4rvKigHhBuZWeP3r");
        var user = PhantasmaKeys.Generate();
        var ownerBalanceBefore = GetBalance(owner.Address.ToString(), "KCAL");

        var sb = ScriptUtils.BeginScript()
            .AllowGas()
            .CallInterop("Runtime.MintTokens", "S3dP2jjf1jUG9nethZBWbnu9a6dFqB7KveTWU7znis6jpDy", owner.Address, "KCAL", 1000000)
            .SpendGas();
        var script = sb.EndScript();
        var tx = new Transaction(
            "simnet",
            DomainSettings.RootChainName,
            script,
            owner.Address,
            owner.Address,
            100000,
            999,
            Timestamp.Now + TimeSpan.FromDays(300),
            "IntegrationTest");
        tx.Mine(ProofOfWork.Minimal);
        tx.Sign(owner);

        var txString = Base16.Encode(tx.ToByteArray(true));
        var txHash = PhantasmaService.SendRawTx.SendRequestAsync(txString, "1").GetAwaiter().GetResult();

        Thread.Sleep(2000);
        var txResult = PhantasmaService.GetTxByHash.SendRequestAsync(txHash, "1").GetAwaiter().GetResult();
        txResult.State.ShouldBe("Fault");

        var balance = GetBalance(user.Address.ToString(), "KCAL");
        // empty address no balance yet
        balance.Valid.ShouldBeFalse();

        var ownerBalanceAfter = GetBalance(owner.Address.ToString(), "KCAL");

        var evt = GetEvents(EventKind.Error, txResult);
        var evtContent = evt.Event.GetContent<string>();
        evtContent.ShouldBe("Minting system token KCAL not allowed");

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
                return (new Event(evt.EventKind, Phantasma.Core.Cryptography.Address.FromText(evt.EventAddress), evt.Contract, Base16.Decode(evt.Data)), true);
            }
        }

        return (default(Event), false);
    }

    private (BigInteger Amount, bool Valid) GetBalance(string address, string token)
    {
        var account = PhantasmaService.GetAccount.SendRequestAsync(address, "1").GetAwaiter().GetResult();

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

    public IntegrationTests()
    {
        Console.WriteLine("Init start");
        //this.DockerClient = new DockerClientConfiguration( new Uri("unix:///var/run/docker.sock")) .CreateClient();
        this.DockerClient = new DockerClientConfiguration( new Uri("npipe://./pipe/docker_engine")) .CreateClient();
        var containers = this.DockerClient.Containers.ListContainersAsync(new ContainersListParameters { All = true }).GetAwaiter().GetResult();

        var existingContainer = containers.Where( x => x.Names.Contains("/PhantasmaIntegrationTest")).FirstOrDefault();
        Console.WriteLine("existingContainer " + existingContainer?.ID);

        if (existingContainer == null)
        {
            CreateContainerResponse container = DockerClient.Containers.CreateContainerAsync(
                    new CreateContainerParameters()
            {
                Image = "phantasma-devnet",
                Name = "PhantasmaIntegrationTest",
                ExposedPorts = new Dictionary<string, EmptyStruct>() {
                    { "5101", new EmptyStruct() }
                },
                HostConfig = new HostConfig
                    {
                        PortBindings = new Dictionary<string, IList<PortBinding>> {
                            {
                                "5101", new List<PortBinding> {
                                    new PortBinding { HostPort = "5101" }
                                }
                            }
                        }
                    }
            }).GetAwaiter().GetResult();

            this.CurrentContainerId = container.ID;
        }
        else
        {
            this.CurrentContainerId = existingContainer.ID;
        }

        var started = DockerClient.Containers.StartContainerAsync(
            this.CurrentContainerId,
            new ContainerStartParameters { },
            CancellationToken.None).GetAwaiter().GetResult();

        if (!started)
        {
            Console.WriteLine("not started");
        }

        // give applications time to start
        Thread.Sleep(10000);
        this.PhantasmaService = new PhantasmaRpcService(new RPCClient(new Uri("http://localhost:5101/rpc"), httpClientHandler: new HttpClientHandler { }));
        Console.WriteLine("Init end");
    }

    public void Dispose()
    {
        Console.WriteLine("Dispose start");
        if (!string.IsNullOrEmpty(this.CurrentContainerId))
        {
            Console.WriteLine("Stop container " + this.CurrentContainerId);
            DockerClient.Containers.KillContainerAsync(
                this.CurrentContainerId,
                new ContainerKillParameters
                {
                    //Signal = "SIGTERM"
                    Signal = "SIGKILL"
                },
                CancellationToken.None).GetAwaiter().GetResult();

            //Console.WriteLine("Remove container " + this.CurrentContainerId);
            //DockerClient.Containers.RemoveContainerAsync(
            //    this.CurrentContainerId,
            //    new ContainerRemoveParameters
            //    {
            //        Force = true
            //    },
            //    CancellationToken.None).GetAwaiter().GetResult();
        }

        this.DockerClient.Dispose();
    }
}
