using System;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Core.Domain;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Node.Oracles;

using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class OracleTests
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
    StakeReward reward;

    public void Initialize()
    {
        user = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        reward = new StakeReward(user.Address, Timestamp.Now);
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
    public void OracleTestNoData()
    {
        Initialize();
        
        var wallet = PhantasmaKeys.Generate();

        nexus.CreatePlatform(nexus.RootStorage, "", wallet.Address, "neo", "GAS");

        for (var i = 0; i < 100; i++)
        {
            var url = DomainExtensions.GetOracleBlockURL("neo", "neoEmpty", new BigInteger(i));
            var iBlock = nexus.GetOracleReader().Read<InteropBlock>(DateTime.Now, url);
        }

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, wallet.Address, nexus.RootChain as Chain, "SOUL", 100);
        var block = simulator.EndBlock().First();
        
        Assert.True(block.OracleData.Count() == 0);
        Console.WriteLine("block oracle data: " + block.OracleData.Count());

    }

    [Fact]
    public void OracleTestWithData()
    {
        Initialize();

        var wallet = PhantasmaKeys.Generate();

        nexus.CreatePlatform(nexus.RootStorage, "", wallet.Address, "neo", "GAS");

        simulator.BeginBlock();

        var totalOracleCalls = 5;

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

        //simulator.GenerateTransfer(owner, wallet.Address, nexus.RootChain as Chain, "SOUL", 100);
        var block = simulator.EndBlock().First();
        Assert.True( simulator.LastBlockWasSuccessful());
        
        Console.WriteLine("block oracle data: " + block.OracleData.Count());
        Assert.True(block.OracleData.Count() == totalOracleCalls);
    }

    [Fact]
    public void OracleTestWithTooMuchData()
    {
        Initialize();

        var wallet = PhantasmaKeys.Generate();

        nexus.CreatePlatform(nexus.RootStorage, "", wallet.Address, "neo", "GAS");

        simulator.BeginBlock();

        var totalOracleCalls = 50;

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

        //simulator.GenerateTransfer(owner, wallet.Address, nexus.RootChain as Chain, "SOUL", 100);
        var block = simulator.EndBlock().First();
        Assert.False(simulator.LastBlockWasSuccessful());

        Console.WriteLine("block oracle data: " + block.OracleData.Count());
        Assert.True(block.OracleData.Count() < totalOracleCalls);
    }


    [Fact]
    public void OraclePrice()
    {
        Initialize();
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None,
            () => ScriptUtils.BeginScript()
                .CallInterop("Oracle.Price", "SOUL")
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 999)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None,
            () => ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 999)
                .CallInterop("Oracle.Price", "SOUL")
                .SpendGas(owner.Address)
                .EndScript());
        var block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());

        foreach (var txHash in block.TransactionHashes)
        {
            var blkResult = block.GetResultForTransaction(txHash);
            var vmObj = VMObject.FromBytes(blkResult);
            Console.WriteLine("price: " + vmObj);
        }
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None,
            () => ScriptUtils.BeginScript()
                .CallInterop("Oracle.Price", "SOUL")
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 999)
                .SpendGas(owner.Address)
                .EndScript());
        block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());

        foreach (var txHash in block.TransactionHashes)
        {
            var blkResult = block.GetResultForTransaction(txHash);
            var vmObj = VMObject.FromBytes(blkResult);
            Console.WriteLine("price: " + vmObj);
        }
    }

    [Fact]
    public void OracleData()
    {
        Initialize();
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
            () => ScriptUtils.BeginScript()
                .CallInterop("Oracle.Price", "SOUL")
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
            () => ScriptUtils.BeginScript()
                .CallInterop("Oracle.Price", "KCAL")
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                .SpendGas(owner.Address)
                .EndScript());
        var block1 = simulator.EndBlock().First();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
            () => ScriptUtils.BeginScript()
                .CallInterop("Oracle.Price", "SOUL")
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
            () => ScriptUtils.BeginScript()
                .CallInterop("Oracle.Price", "KCAL")
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                .SpendGas(owner.Address)
                .EndScript());
        var block2 = simulator.EndBlock().First();

        var oData1 = block1.OracleData.Count();
        var oData2 = block2.OracleData.Count();

        Console.WriteLine("odata1: " + oData1);
        Console.WriteLine("odata2: " + oData2);

        
        Assert.True(oData1 == oData2);
    }

}