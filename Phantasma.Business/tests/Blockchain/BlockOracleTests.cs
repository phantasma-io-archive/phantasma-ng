using System;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Numerics;

namespace Phantasma.Business.Tests.Blockchain;

using Xunit;

using Phantasma.Business.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class BlockOracleTests
{
    
    PhantasmaKeys user;
    PhantasmaKeys owner;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;

    public BlockOracleTests()
    {
        Initialize();
    }

    public void Initialize()
    {
        user = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
    
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(owner);
        nexus = simulator.Nexus;
        nexus.SetOracleReader(new OracleSimulator(nexus));
        SetInitialBalance(user.Address);
    }

    protected void SetInitialBalance(Address address)
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, initialAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
    [Fact]
    public void TestBlockOracleInstance()
    {
        var wallet = PhantasmaKeys.Generate();
        nexus.CreatePlatform(nexus.RootStorage, "", wallet.Address, "neo", "GAS");
        var block = nexus.RootChain.GetBlockByHash(nexus.RootChain.GetLastBlockHash());
        var oracle = new BlockOracleReader(nexus, block);

        Assert.NotNull(block);
        Assert.Equal(nexus, oracle.Nexus);
        Assert.Equal(block, oracle.OriginalBlock);
    }

    [Fact(Skip = "Need to update this test")]
    public void TestRead()
    {
        var wallet = PhantasmaKeys.Generate();
        nexus.CreatePlatform(nexus.RootStorage, "", wallet.Address, "neo", "GAS");
        var block = nexus.RootChain.GetBlockByHash(nexus.RootChain.GetLastBlockHash());
        var oracle = new BlockOracleReader(nexus, block);
        nexus.SetOracleReader(oracle);
        
        var totalOracleCalls = 5;
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        {
            var sb = new ScriptBuilder();

            sb.AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit);

            for (var i = 1; i <= totalOracleCalls; i++)
            {
                var url = DomainExtensions.GetOracleBlockURL("neo", "neo", new BigInteger(i));
                sb.CallInterop("Oracle.Read", url);
            }

            sb.TransferBalance("SOUL", owner.Address, wallet.Address);

            sb.SpendGas(owner.Address);

            return sb.EndScript();
        });
        var blockEnd = simulator.EndBlock().First();
        Assert.True( simulator.LastBlockWasSuccessful());

        Assert.NotNull(block);
        Assert.Equal(nexus, oracle.Nexus);
        Assert.Equal(block, oracle.OriginalBlock);
        
        
        Console.WriteLine("block oracle data: " + blockEnd.OracleData.Count());
        Assert.True(blockEnd.OracleData.Count() == totalOracleCalls);
        
        

    }

    [Fact]
    public void TestPullPrice()
    {
        
    }

    [Fact]
    public void TestPullFee()
    {
        
    }
}
