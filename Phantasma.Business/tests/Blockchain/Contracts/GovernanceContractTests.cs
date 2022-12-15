using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

[Collection(nameof(SystemTestCollectionDefinition))]
public class GovernanceContractTests
{
    Address sysAddress;
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

    public GovernanceContractTests()
    {
        Initialize();
    }

    private void Initialize()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Friends);
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
    public void TestGovernance()
    {
        var token = nexus.GetTokenInfo(nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);
        var KcalAmount = UnitConversion.ToBigInteger(1000, DomainSettings.FuelTokenDecimals);
        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);
        var name = "name.gov.test";
        var value = 5;
        var hash = new Hash(CryptoExtensions.Sha256("TestHash"));

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, KcalAmount);
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();
        
        // Create Value 
        var value2 = 10;
        var constraintMin = new ChainConstraint
        {
            Kind = ConstraintKind.MinValue,
            Tag = "MinValue",
            Value = 1,
        };
        var constraintMax = new ChainConstraint
        {
            Kind = ConstraintKind.MaxValue,
            Tag = "MaxValue",
            Value = 100,
        };
        
        var constraints = new ChainConstraint[]
        {
            constraintMin,
            constraintMax,
        };
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Governance, nameof(GovernanceContract.CreateValue), owner.Address, name, value2, constraints.Serialize())
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // GetNames
        var names2 = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.GetNames)).AsStruct<string[]>();
        Assert.Contains(name, names2);
        
        // GetValues
        var values2 = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.GetValues)).AsStruct<GovernancePair[]>();
        var governancePair2 = values2.First(x => x.Name == name && x.Value == value2);
        Assert.Equal(name, governancePair2.Name);
        Assert.Equal(value2, governancePair2.Value);
        
        // GetValue - (string name)
        var result = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.GetValue), name).AsNumber();
        Assert.Equal(value2, result);
        
        // HasValue - (string name)
        var hasValue = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.HasValue), name).AsBool();
        Assert.True(hasValue);
        
        // HasName - (string name)
        var hasName = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.HasName), name).AsBool();
        Assert.True(hasName);
        
        // GetNames
        var names = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.GetNames)).AsStruct<string[]>();
        Assert.Contains(name, names);
        
        // GetValues
        var values = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.GetValues)).AsStruct<GovernancePair[]>();
        var governancePair = values.First(x => x.Name == name && x.Value == value2);
        Assert.Equal(name, governancePair.Name);
        Assert.Equal(value2, governancePair.Value);
        
        // SetValue - (Address from, string name, BigInteger value) -- This Will fail cause of consensus....
        /*
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Governance, nameof(GovernanceContract.SetValue), owner.Address, name, value)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
        
        // GetValue - (string name)
        var result = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.GetValue), name).AsNumber();
        Assert.Equal(value, result);
        
        // HasValue - (string name)
        var hasValue = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.HasValue), name).AsBool();
        Assert.True(hasValue);
        
        // HasName - (string name)
        var hasName = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.HasName), name).AsBool();
        Assert.True(hasName);
        
        // GetNames
        var names = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.GetNames)).AsStruct<string[]>();
        Assert.Contains(name, names);
        
        // GetValues
        var values = simulator.InvokeContract(NativeContractKind.Governance, nameof(GovernanceContract.GetValues)).AsStruct<GovernancePair[]>();
        var governancePair = values.First(x => x.Name == name && x.Value == value);
        Assert.Equal(name, governancePair.Name);
        Assert.Equal(value, governancePair.Value);*/
        
        

    }
    
    // HasName
    // GetNames
    // GetValues
    // HasValue
    // CreateValue
    // GetValue
    // SetValue
}
