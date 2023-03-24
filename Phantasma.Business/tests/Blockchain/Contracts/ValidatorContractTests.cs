using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

[Collection(nameof(SystemTestCollectionDefinition))]
public class ValidatorContractTests
{
    // TODO: Implement tests for ValidatorContract
     Address sysAddress;
    PhantasmaKeys user;
    PhantasmaKeys owner;
    PhantasmaKeys owner2;
    PhantasmaKeys owner3;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;
    StakeReward reward;

    public ValidatorContractTests()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);
        user = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        owner2 = PhantasmaKeys.Generate();
        owner3 = PhantasmaKeys.Generate();
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
        simulator = new NexusSimulator(new []{owner, owner2, owner3}, DomainSettings.LatestKnownProtocol);
        nexus = simulator.Nexus;
        nexus.SetOracleReader(new OracleSimulator(nexus));
        simulator.GetFundsInTheFuture(owner);
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 9999)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), owner.Address)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
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
    public void TestValidatorContract()
    {
        
        /*simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Validator, nameof(ValidatorContract.GetMaxTotalValidators), owner.Address, DomainSettings.StakingTokenSymbol, 100000000)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());*/

        var maxValidators = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetMaxTotalValidators)).AsNumber();
        
        Assert.Equal(maxValidators, 4);
        
        var primaryValidators = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetMaxPrimaryValidators)).AsNumber();
        
        Assert.Equal(1, primaryValidators);
        
        var secondaryValidators = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetMaxSecondaryValidators)).AsNumber();
        
        Assert.Equal(3, secondaryValidators);
        
        var primaryValidatorsByType = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorCount), ValidatorType.Primary).AsNumber();
        
        Assert.Equal(3, primaryValidatorsByType);
        
        var secondaryValidatorsByType = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorCount), ValidatorType.Secondary).AsNumber();
        
        Assert.Equal(0, secondaryValidatorsByType);
        
        var validatorType = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorType), owner.Address).AsEnum<ValidatorType>();
        
        Assert.Equal(ValidatorType.Primary, validatorType);
        
        var validatorType2 = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorType), Address.Null).AsEnum<ValidatorType>();
        
        Assert.Equal(ValidatorType.Invalid, validatorType2);
        
        var validatorIndex = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetIndexOfValidator), Address.Null).AsNumber();
        
        Assert.Equal(-1, validatorIndex);
        
        validatorIndex = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetIndexOfValidator), owner.Address).AsNumber();
        
        Assert.Equal(0, validatorIndex);
        
        var currentValidator = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, simulator.CurrentTime,
            NativeContractKind.Validator, nameof(ValidatorContract.GetCurrentValidator), owner.Address.TendermintAddress).AsStruct<ValidatorEntry>();
        
        Assert.Equal(owner.Address, currentValidator.address);
        Assert.Equal(ValidatorType.Primary, currentValidator.type);
        
    }

    [Fact]
    public void SetValidatorTest()
    {
        
    }
}
