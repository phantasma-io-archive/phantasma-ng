using System.Numerics;
using System.Text;
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
public class BlockContractTests
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

    public BlockContractTests()
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
    public void TestBlock()
    {
        var token = nexus.GetTokenInfo(nexus.StorageCollection.ContractsStorage, DomainSettings.StakingTokenSymbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);
        var KcalAmount = UnitConversion.ToBigInteger(1000, DomainSettings.FuelTokenDecimals);
        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);
        var platform = "test";
        var hash = new Hash(CryptoExtensions.Sha256("TestsHash2")); 
        
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, KcalAmount);
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();
        
        // Settle Transaction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Block, nameof(BlockContract.SettleTransaction), user.Address, hash)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        var isSettled = simulator.InvokeContract(NativeContractKind.Block, nameof(BlockContract.IsSettled), hash).AsBool();
        Assert.False(isSettled);
    }
    
    // IsSettled
    // RegisterHashAsKnown
    // DoSettlement
    // SettleTransaction
    
}
