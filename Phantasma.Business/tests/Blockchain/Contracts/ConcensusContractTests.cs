using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

using Xunit;

using Phantasma.Business.Blockchain.Contracts;

[Collection(nameof(SystemTestCollectionDefinition))]
public class ConcensusContractTests
{
    PhantasmaKeys user;
    PhantasmaKeys user2;
    PhantasmaKeys user3;
    PhantasmaKeys owner;
    PhantasmaKeys owner2;
    PhantasmaKeys owner3;
    PhantasmaKeys owner4;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;

    public ConcensusContractTests()
    {
        Initialize();
    }

    public void Initialize()
    {
        user = PhantasmaKeys.Generate();
        user2 = PhantasmaKeys.Generate();
        user3 = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        owner2 = PhantasmaKeys.Generate();
        owner3 = PhantasmaKeys.Generate();
        owner4 = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
    
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(new []{owner, owner2, owner3, owner4});
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
    public void TestMigrate()
    {
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.Migrate), user.Address, user2.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
    }
    
    [Fact]
    public void TestConsensus()
    {
        //  InitPoll(Address from, string subject, string organization, ConsensusMode mode, Timestamp startTime, Timestamp endTime, byte[] serializedChoices, BigInteger votesPerUser)
        var subject = "subject_test";
        var organization = DomainSettings.ValidatorsOrganizationName;
        var mode = ConsensusMode.Majority;
        var startTime = (Timestamp)(Timestamp.Now.Value + 100);
        var endTime = (Timestamp) (startTime.Value + 100000);
        // Choices PollChoice
        var choices = new PollChoice[]
        {
            new PollChoice(Encoding.UTF8.GetBytes("choice1")),
            new PollChoice(Encoding.UTF8.GetBytes("choice2")),
            new PollChoice(Encoding.UTF8.GetBytes("choice3")),
        };
        var serializedChoices = choices.Serialize();
        var votesPerUser = 1;
        
        // Init Pool
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.InitPoll), user.Address,subject, organization, mode, startTime, endTime, serializedChoices, votesPerUser)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Try to Init Again to check the Fetch pool
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.InitPoll), user.Address,subject, organization, mode, startTime, endTime, serializedChoices, votesPerUser)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        simulator.TimeSkipHours(1);
        
        Thread.Sleep(1000);
        
        // Let's vote with owner
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.SingleVote), owner.Address, subject, 0)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        Assert.Throws<ChainException>(() => simulator.InvokeContract(NativeContractKind.Consensus,
            nameof(ConsensusContract.HasConsensus), subject, choices[0].value));
        
        simulator.TimeSkipDays(2);

        // Check consensus it needs to be a transaction so it can alter the state of the chain
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.HasConsensus), subject, choices[0].value)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        var hasConsensus = simulator.InvokeContract(NativeContractKind.Consensus,
            nameof(ConsensusContract.HasConsensus), subject, choices[0].value).AsBool();
        Assert.True(hasConsensus);
    }
    
    [Fact]
    public void PollChoice_Value_IsSet()
    {
        // Arrange
        byte[] expectedValue = new byte[] { 0x01, 0x02, 0x03 };
        PollChoice pollChoice = new PollChoice(expectedValue);

        // Act
        byte[] actualValue = pollChoice.value;

        // Assert
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void TestMultisignature()
    {
        var subject = "subject_test";
        var nexusName = "simnet";
        var chainName = "main";
        var script = ScriptUtils.BeginScript()
            .AllowGas(owner3.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
            .CallInterop("Runtime.TransferTokens", owner.Address, owner2.Address, DomainSettings.StakingTokenSymbol, amountRequested )
            .SpendGas(owner3.Address)
            .EndScript(); // TODO: Change to a valid script to test if they have permission to perform this.
        var time = simulator.CurrentTime;
        var payload = "Consensus";
        time = time + TimeSpan.FromHours(12);

        var transaction = new Transaction(nexusName, chainName, script, time, payload);
        transaction.Sign(owner);
        List<Address> addresses = new List<Address>();
        addresses.Add(owner.Address);
        addresses.Add(owner2.Address);
        addresses.Add(owner3.Address);
        addresses.Add(owner4.Address);
        
        // Create Transaction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.CreateTransaction), owner.Address, subject, Serialization.Serialize(transaction), addresses.ToArray())
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var signature = transaction.GetTransactionSignature(owner2);
        transaction.AddSignature(signature);

        // Try to Init Again to check the Fetch pool
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.AddSignatureTransaction), owner2.Address, subject, signature.Serialize())
                .SpendGas(owner2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        signature = transaction.GetTransactionSignature(owner3);
        transaction.AddSignature(signature);


        simulator.TimeSkipHours(1);
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner3, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner3.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.AddSignatureTransaction), owner3.Address, subject, Serialization.Serialize(signature))
                .SpendGas(owner3.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        signature = transaction.GetTransactionSignature(owner4);
        transaction.AddSignature(signature);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner4, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner4.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.AddSignatureTransaction), owner4.Address, subject, Serialization.Serialize(signature))
                .SpendGas(owner4.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // Get the transaction
        simulator.BeginBlock();
        var tx = simulator.GenerateCustomTransaction(owner4, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner4.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.GetTransaction), owner4.Address, subject)
                .SpendGas(owner4.Address)
                .EndScript());
        var block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());
        var txResult = block.GetResultForTransaction(tx.Hash);
        Assert.NotNull(txResult);

        var test = Serialization.Unserialize<VMObject>(txResult);
        var toTransactionBytes = test.AsByteArray();
        var result = Transaction.Unserialize(toTransactionBytes); 
        Assert.NotNull(result);
        
        Assert.Equal(transaction.Expiration, result.Expiration);
        Assert.Equal(transaction.Payload, result.Payload);
        Assert.Equal(transaction.Script, result.Script);
        Assert.Equal(transaction.NexusName, result.NexusName);
        Assert.Equal(transaction.ChainName, result.ChainName);
        Assert.Equal(transaction.Signatures.Length, result.Signatures.Length);
        Assert.Equal(transaction.Signatures[0].Kind, result.Signatures[0].Kind);
        Assert.Equal(transaction.Signatures[1].Kind, result.Signatures[1].Kind);
        Assert.Equal(transaction.Signatures[2].Kind, result.Signatures[2].Kind);
        Assert.Equal(transaction.Signatures[3].Kind, result.Signatures[3].Kind);

        simulator.BeginBlock();
        simulator.SendRawTransaction(result);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Delete transaction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner4, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner4.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.DeleteTransaction), addresses.ToArray(), subject)
                .SpendGas(owner4.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        
        // Validate transaction is deleted
        // Get the transaction
        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(owner4, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner4.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.GetTransaction), owner4.Address, subject)
                .SpendGas(owner4.Address)
                .EndScript());
        block = simulator.EndBlock().First();
        Assert.False(simulator.LastBlockWasSuccessful());
        txResult = block.GetResultForTransaction(tx.Hash);
        Assert.Null(txResult);
    }
}
