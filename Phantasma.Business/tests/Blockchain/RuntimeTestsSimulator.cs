using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Tasks;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Numerics;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]

public class RuntimeTestsSimulator
{
    Address sysAddress;
    PhantasmaKeys user;
    PhantasmaKeys owner;
    PhantasmaKeys owner2;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;

    public RuntimeTestsSimulator()
    {
        Initialize();
    }

    private void Initialize()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Friends);
        user = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        owner2 = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
        
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(new []{owner, owner2});
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
    public void TestRuntime()
    {
        RuntimeVM runtime;
        var script = new byte[0];
        var offset = (uint)1;
        var transaction = new Transaction();
        var hash = new Hash(CryptoExtensions.Sha256("test"));
        runtime = new RuntimeVM(0, script, offset, nexus.RootChain,  owner.Address, simulator.CurrentTime, transaction, nexus.RootChain.CurrentChangeSet, nexus.GetOracleReader(), null);
        Assert.Equal(Transaction.Null, runtime.GetTransaction(hash));
        Assert.Equal(new string[0], runtime.GetFeeds());
        Assert.Equal(new string[0], runtime.GetPlatforms());
        Assert.NotEqual(new string[0], runtime.GetTokens());
        Assert.Equal(new string[]{"main"}, runtime.GetChains());
        Assert.Null(runtime.GetTask(0));
        Assert.Null(runtime.GetChainByAddress(sysAddress));
        Assert.Equal( $"Runtime.Context=",runtime.ToString());
        Assert.Throws<NotImplementedException>(() => runtime.GetContract("exchange").Name);
        Assert.NotEmpty(runtime.GetContracts());
        Assert.Equal(owner.Address.Text, runtime.GetContractOwner(sysAddress).Text);
        Assert.Equal(0, runtime.GetIndexOfChain("main"));
        Assert.Throws<NullReferenceException>(() => runtime.GetChainParent("main"));
        Assert.Equal(SmartContract.GetAddressForNative(NativeContractKind.Exchange).Text, runtime.LookUpName("exchange").Text);
        Assert.Equal(true, runtime.TokenExists("NEO", "neo"));
        Assert.NotEmpty(runtime.GetValidators());
        Assert.Equal(2, runtime.GetPrimaryValidatorCount());
        Assert.Throws<ChainException>(() =>runtime.GetPlatformByName("eth"));
        Assert.Null(runtime.GetPlatformByIndex(0));
        Assert.Null(runtime.GetPlatformByIndex(-1));
        //Assert.Throws<VMException>(() => runtime.Throw("asdasd"));
        Assert.Throws<ChainException>(() => runtime.GetFeed("asdas"));
        Assert.Throws<NullReferenceException>( () => runtime.IsCurrentContext(new ChainExecutionContext(null)));
        Assert.NotNull(runtime.IsNameOfChildChain("main"));
        Assert.NotNull(runtime.IsNameOfParentChain("main"));
        Assert.NotNull(runtime.IsAddressOfChildChain(Address.Null));
        Assert.NotNull(runtime.IsAddressOfParentChain(Address.Null));
        Assert.NotNull(runtime.GetSecondaryValidatorCount());
        Assert.NotNull(runtime.ReadOracle("price://SOUL"));
        Assert.Throws<Exception>(() => runtime.ReadToken("CROWN", 0));
        Assert.NotNull(runtime.GetValidatorByIndex(0));
        //string sourceChain, Address from, string targetChain, Address to, string symbol, BigInteger value
        //Assert.Throws<ArgumentNullException>(() =>runtime.SwapTokens("phantasma", owner.Address, "neo", user.Address, "SOUL", 10000));
        
        Assert.NotNull(runtime.GetAddressScript(owner.Address));
        Assert.NotNull(runtime.HasAddressScript(owner.Address));
        Assert.NotNull(runtime.GetTransactionHashesForAddress(owner.Address));
        Assert.NotNull(runtime.IsSecondaryValidator(owner.Address));
        //Address from, string contractName, ContractMethod method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit)
        var dict = new Dictionary<string, int>();
        dict.Add("test", 1);
        var contractMethod = new ContractMethod("test", VMType.Number, dict);
        Assert.Throws<EndOfStreamException>(() =>runtime.StartTask(owner.Address, "exchange", ContractMethod.FromBytes(new byte[0]), 0, 1, TaskFrequencyMode.Blocks, 10));

        
    }
}
