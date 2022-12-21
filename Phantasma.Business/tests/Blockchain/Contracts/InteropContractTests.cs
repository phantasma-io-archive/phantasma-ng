using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Node.Chains.Ethereum;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

[Collection(nameof(SystemTestCollectionDefinition))]
public class InteropContractTests
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

    public InteropContractTests()
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
    public void TestRegisterAddress()
    {
        var token = nexus.GetTokenInfo(nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);
        var KcalAmount = UnitConversion.ToBigInteger(1000, DomainSettings.FuelTokenDecimals);
        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);
        var platform = "test";
        var externalAddress = EthereumKey.Generate();
        var hash = new Hash(CryptoExtensions.Sha256("TestHash"));

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, KcalAmount);
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();
        
        // Register address - (Address from, string platform, Address localAddress, string externalAddress)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Interop, nameof(InteropContract.RegisterAddress), user.Address, platform, user.Address, externalAddress.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
        
        // Get address - (Address from, string platform, string chain, Hash hash)
        /*var status = simulator.InvokeContract(NativeContractKind.Interop, nameof(InteropContract.GetStatus), platform,
            hash); //.AsEnum<InteropTransferStatus>();
        
        //Assert.Equal(InteropTransferStatus.Pending, status);
        
        var swapsForAddress = simulator.InvokeContract(NativeContractKind.Interop, nameof(InteropContract.GetSwapsForAddress), user.Address).AsStruct<InteropHistory[]>();
        Assert.Single(swapsForAddress);*/

        // GetSwapsForAddress
        // GetStatus
        // GetSettlement
        // WithdrawTokens
        // SettleTransaction
    }
    
}
