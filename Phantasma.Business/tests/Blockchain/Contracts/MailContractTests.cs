using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

using Xunit;
using Phantasma.Business.Blockchain.Contracts.Native;

[Collection(nameof(SystemTestCollectionDefinition))]
public class MailContractTests
{
    PhantasmaKeys user;
    PhantasmaKeys user2;
    PhantasmaKeys user3;
    PhantasmaKeys owner;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;

    public MailContractTests()
    {
        Initialize();
    }

    public void Initialize()
    {
        user = PhantasmaKeys.Generate();
        user2 = PhantasmaKeys.Generate();
        user3 = PhantasmaKeys.Generate();
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
        SetInitialBalance(user2.Address);
        SetInitialBalance(user3.Address);
    }

    protected void SetInitialBalance(Address address)
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, initialAmount);
        simulator.EndBlock();
        
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 100000000000);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
    [Fact]
    public void TestRegisterDomain()
    {
        var domainName = "testDomain";
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.RegisterDomain), user.Address, domainName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
    [Fact]
    public void TestUnRegisterDomain()
    {
        var domainName = "testDomain";
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.RegisterDomain), user.Address, domainName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        var domainExists = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.DomainExists), domainName).AsBool();
        
        Assert.True(domainExists);
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.UnregisterDomain), domainName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        domainExists = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.DomainExists), domainName).AsBool();
        
        Assert.False(domainExists);
    }
    
    [Fact(Skip = "Not implemented verification")]
    public void TestRegisterDomainWithInvalidName()
    {
        var domainName = "Inval3123+'12e.asd-._:_:;_D;ASDOAPSDJ=)ASDJidaDomainNameForASdaosdjoasdjoasjdo1234o12j4o1j2o4j___-asd-as12301``*^^Tests";
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.RegisterDomain), user.Address, domainName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
        
        var domainExists = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.DomainExists), domainName).AsBool();
        
        Assert.False(domainExists);
    }
    
    [Fact]
    public void TestJoinDomain()
    {
        var domainName = "testDomain";
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.RegisterDomain), user.Address, domainName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        var domainExists = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.DomainExists), domainName).AsBool();
        var domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();

        Assert.True(domainExists);
        Assert.Equal(1, domainMembers.Count());
        
        // Join the domain with another user
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.JoinDomain), user2.Address, domainName)
                .SpendGas(user2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();
        Assert.Equal(2, domainMembers.Count());
    }
    
    [Fact]
    public void TestLeaveDomain()
    {
        var domainName = "testDomain";
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.RegisterDomain), user.Address, domainName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        var domainExists = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.DomainExists), domainName).AsBool();
        var domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();

        Assert.True(domainExists);
        Assert.Equal(1, domainMembers.Count());
        
        // Join the domain with another user
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.JoinDomain), user2.Address, domainName)
                .SpendGas(user2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();
        Assert.Equal(2, domainMembers.Count());
        
        // Leave the domain
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.LeaveDomain), user2.Address, domainName)
                .SpendGas(user2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();
        Assert.Equal(1, domainMembers.Count());
    }
    
    [Fact(Skip = "Migrate domain not implemented correctly")]
    public void TestMigrateDomain()
    {
        var domainName = "testDomain";
        var domainName2 = "testDomain2";
        // Create domain
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.RegisterDomain), user.Address, domainName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user3, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user3.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.RegisterDomain), user3.Address, domainName2)
                .SpendGas(user3.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        
        var domainExists = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.DomainExists), domainName).AsBool();
        var domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();

        Assert.True(domainExists);
        Assert.Equal(1, domainMembers.Count());
        
        // Join the domain with another user
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.JoinDomain), user2.Address, domainName)
                .SpendGas(user2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();
        Assert.Equal(2, domainMembers.Count());
        
        // Migrate the domain
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.MigrateDomain), domainName2, user.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();
        Assert.Equal(1, domainMembers.Count());
    }

    [Fact]
    public void TestGetUserDomain()
    {
        var domainName = "testDomain";
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.RegisterDomain), user.Address, domainName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        var domainExists = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.DomainExists), domainName).AsBool();
        var domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();
        
        Assert.True(domainExists);
        Assert.Equal(1, domainMembers.Count());
        
        // Join the domain with another user
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.JoinDomain), user2.Address, domainName)
                .SpendGas(user2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();
        var userDomain = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetUserDomain), user.Address).AsString();

        Assert.Equal(2, domainMembers.Count());
        Assert.Equal(domainName, userDomain);
        
        // Leave the domain
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Mail, nameof(MailContract.LeaveDomain), user2.Address, domainName)
                .SpendGas(user2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        domainMembers = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetDomainUsers), domainName).ToArray<Address>();
        userDomain = simulator.InvokeContract(NativeContractKind.Mail, nameof(MailContract.GetUserDomain), user2.Address).AsString();
        Assert.Equal(1, domainMembers.Count());
        Assert.Equal("", userDomain);
    }
    
    // PushMessage
    // MigrateDomain
    
}
