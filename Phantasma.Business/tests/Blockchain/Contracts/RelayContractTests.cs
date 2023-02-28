using System;
using System.IO;
using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.EdDSA;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

using Xunit;
using Phantasma.Business.Blockchain.Contracts.Native;

[Collection(nameof(SystemTestCollectionDefinition))]
public class RelayContractTests
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

    public RelayContractTests()
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
    public void TestToByteArray()
    {
        // Create a new RelayMessage instance
        var relayMessage = new RelayMessage
        {
            nexus = "test",
            index = 123,
            timestamp = new Timestamp(12345),
            sender = PhantasmaKeys.Generate().Address,
            receiver = PhantasmaKeys.Generate().Address,
            script = new byte[] { 1, 2, 3, 4 }
        };

        // Call the ToByteArray method
        var bytes = relayMessage.ToByteArray();

        // Assert that the method returns a non-null byte array
        Assert.NotNull(bytes);

        // Assert that the length of the byte array is what we expect
        Assert.Equal(87, bytes.Length);

        // Use a BinaryReader to read the data from the byte array
        using (var reader = new BinaryReader(new MemoryStream(bytes)))
        {
            // Assert that the nexus value is correct
            Assert.Equal("test", reader.ReadVarString());

            // Assert that the index value is correct
            Assert.Equal(123, reader.ReadBigInteger());

            // Assert that the timestamp value is correct
            Assert.Equal((uint)12345, reader.ReadUInt32());

            // Assert that the sender value is correct
            Assert.Equal(relayMessage.sender.Text, reader.ReadAddress().Text);

            // Assert that the receiver value is correct
            Assert.Equal(relayMessage.receiver.Text, reader.ReadAddress().Text);

            // Assert that the script value is correct
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, reader.ReadByteArray());
        }
    }

    [Fact]
    public void TestSerializeData()
    {
        // Create a new RelayMessage instance
        var relayMessage = new RelayMessage
        {
            nexus = "test",
            index = 123,
            timestamp = new Timestamp(12345),
            sender = PhantasmaKeys.Generate().Address,
            receiver = PhantasmaKeys.Generate().Address,
            script = new byte[] { 1, 2, 3, 4 }
        };

        // Use a MemoryStream to store the serialized data
        var bytes = new byte[128];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        relayMessage.SerializeData(writer);

        stream.Position = 0;

        var reader = new BinaryReader(stream);
        
        // Assert that the nexus value is correct
        Assert.Equal("test", reader.ReadVarString());

        // Assert that the index value is correct
        Assert.Equal(123, reader.ReadBigInteger());

        // Assert that the timestamp value is correct
        Assert.Equal((uint)12345, reader.ReadUInt32());
        
        // Assert that the sender value is correct
        Assert.Equal(relayMessage.sender.Text, reader.ReadAddress().Text);

        // Assert that the receiver value is correct
        Assert.Equal(relayMessage.receiver.Text, reader.ReadAddress().Text);

        // Assert that the script value is correct
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, reader.ReadByteArray());
            
    }
    
    [Fact]
    public void TestRelayReceiptSerialization()
    {
        // Arrange
        var keys = PhantasmaKeys.Generate();

        var msg = new RelayMessage
        {
            nexus = "test",
            index = 123,
            timestamp = new Timestamp(12345),
            sender = keys.Address,
            receiver = PhantasmaKeys.Generate().Address,
            script = new byte[] { 1, 2, 3, 4 }
        };
        var receipt = RelayReceipt.FromMessage(msg, keys);
        var expectedBytes = receipt.ToArray();

        // Act
        var actualReceipt = RelayReceipt.FromBytes(expectedBytes);

        // Assert
        Assert.Equal(receipt.message.index, actualReceipt.message.index);
        Assert.Equal(receipt.message.nexus, actualReceipt.message.nexus);
        Assert.Equal(receipt.message.receiver, actualReceipt.message.receiver);
        Assert.Equal(receipt.message.sender, actualReceipt.message.sender);
        Assert.Equal(receipt.message.script, actualReceipt.message.script);
        Assert.Equal(receipt.message.timestamp, actualReceipt.message.timestamp);
        Assert.Equal(receipt.signature.ToByteArray(), actualReceipt.signature.ToByteArray());
    }

    [Fact]
    public void TestRelayReceiptFromMessage()
    {
        // Arrange
        var keys = PhantasmaKeys.Generate();

        var msg = new RelayMessage
        {
            nexus = "test",
            index = 123,
            timestamp = new Timestamp(12345),
            sender = keys.Address,
            receiver = PhantasmaKeys.Generate().Address,
            script = new byte[] { 1, 2, 3, 4 }
        };
        
        var expectedReceipt = new RelayReceipt()
        {
            message = msg,
            signature = Ed25519Signature.Generate(keys, msg.ToByteArray())
        };

        // Act
        var actualReceipt = RelayReceipt.FromMessage(msg, keys);

        // Assert
        Assert.Equal(expectedReceipt.ToArray(), actualReceipt.ToArray());
        Assert.Equal(expectedReceipt.message.ToByteArray(), actualReceipt.message.ToByteArray());
        Assert.Equal(expectedReceipt.message.index, actualReceipt.message.index);
        Assert.Equal(expectedReceipt.message.nexus, actualReceipt.message.nexus);
        Assert.Equal(expectedReceipt.message.receiver, actualReceipt.message.receiver);
        Assert.Equal(expectedReceipt.message.sender, actualReceipt.message.sender);
        Assert.Equal(expectedReceipt.message.script, actualReceipt.message.script);
        Assert.Equal(expectedReceipt.message.timestamp, actualReceipt.message.timestamp);
        Assert.Equal(expectedReceipt.signature.ToByteArray(), actualReceipt.signature.ToByteArray());
    }

    [Fact]
    public void TestRelayReceiptFromMessageWithEmptyScript()
    {
        // Arrange
        var msg = new RelayMessage()
        {
            script = new byte[0]
        };
        var keys = PhantasmaKeys.Generate();

        // Act and Assert
        Assert.Throws<Exception>(() => RelayReceipt.FromMessage(msg, keys));
    }
    
    [Fact]
    public void TestRelayReceiptFromMessageWithNullScript()
    {
        // Arrange
        var msg = new RelayMessage()
        {
            script = null
        };
        var keys = PhantasmaKeys.Generate();

        // Act and Assert
        Assert.Throws<Exception>(() => RelayReceipt.FromMessage(msg, keys));
    }

    [Fact]
    public void RelayContractFull()
    {
        var token = nexus.GetTokenInfo(nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);
        var KcalAmount = UnitConversion.ToBigInteger(1000, DomainSettings.FuelTokenDecimals);
        var numberOfMessages = 5;
        var expected = RelayContract.RelayFeePerMessage * numberOfMessages;

        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);
        
        // Setup Balances
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, KcalAmount);
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();
        
        // Get Balance
        var balance = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetBalance), user.Address).AsNumber();
        Assert.Equal(0, balance);
        
        // Open Channel
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.OpenChannel), user.Address, user.PublicKey)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Add Balance
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.TopUpChannel), user.Address, numberOfMessages)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Get Balance
        balance = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetBalance), user.Address).AsNumber();
        Assert.Equal(expected, balance);
        
        // Get Key
        var key = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetKey), user.Address).AsByteArray();
        Assert.Equal(user.PublicKey, key);
        
        // Get Index
        var index = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetIndex), user.Address, user2.Address).AsNumber();
        Assert.Equal(0, index);
        
        // SettleChannel
        // RelayReceipt receipt
        var msg = new RelayMessage
        {
            nexus = "simnet",
            index = 0,
            timestamp = simulator.CurrentTime,
            sender = user.Address,
            receiver = user2.Address,
            script = new byte[] { 1, 2, 3, 4 }
        };
        
        var receipt = RelayReceipt.FromMessage(msg, user);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.SettleChannel), receipt)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Get Index
        index = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetIndex), user.Address, user2.Address).AsNumber();
        Assert.Equal(1, index);

    }

    [Fact]
    public void RelayContractGetTopAddress()
    {
        var token = nexus.GetTokenInfo(nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);
        var KcalAmount = UnitConversion.ToBigInteger(1000, DomainSettings.FuelTokenDecimals);
        var numberOfMessages = 5;
        var expectedPrice = RelayContract.RelayFeePerMessage * numberOfMessages;
        var expectedAddress = Address.FromHash(Encoding.UTF8.GetBytes(user.Address.Text+".relay"));

        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);
        
        // Setup Balances
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, KcalAmount);
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();
        
        // Get Balance
        var balance = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetBalance), user.Address).AsNumber();
        Assert.Equal(0, balance);
        
        // Open Channel
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.OpenChannel), user.Address, user.PublicKey)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Add Balance
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.TopUpChannel), user.Address, numberOfMessages)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Get Balance
        balance = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetBalance), user.Address).AsNumber();
        Assert.Equal(expectedPrice, balance);
        
        // Get Top Address
        var topAddress = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetTopUpAddress), user.Address).AsAddress();
        Assert.Equal(expectedAddress, topAddress);
    }

    [Fact]
    public void RelayContractGetBalanceHack()
    {
        var token = nexus.GetTokenInfo(nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);
        var KcalAmount = UnitConversion.ToBigInteger(1000, DomainSettings.FuelTokenDecimals);
        var numberOfMessages = 5;
        var expected = RelayContract.RelayFeePerMessage * numberOfMessages;
        var relayAddress = SmartContract.GetAddressForNative(NativeContractKind.Stake);
        var expectedPrice = RelayContract.RelayFeePerMessage * numberOfMessages;

        
        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);
        
        // Setup Balances
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, KcalAmount);
        simulator.GenerateTransfer(owner, relayAddress, nexus.RootChain, DomainSettings.FuelTokenSymbol, KcalAmount);
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();
        
        // Get Balance
        var balance = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetBalance), user.Address).AsNumber();
        Assert.Equal(0, balance);
        
        // Open Channel
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.OpenChannel), relayAddress, user.PublicKey)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
        
        // Add Balance
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.TopUpChannel), relayAddress, numberOfMessages)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
        
        // Get Balance
        balance = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetBalance), relayAddress).AsNumber();
        Assert.NotEqual(expected, balance);
        
        
        // Method 2 - Create Everything like normal, but then try to get the balance of the relay address
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.OpenChannel), user.Address, user.PublicKey)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Add Balance
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.TopUpChannel), user.Address, numberOfMessages)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Get Balance
        balance = simulator.InvokeContract(NativeContractKind.Relay, nameof(RelayContract.GetBalance), user.Address).AsNumber();
        Assert.Equal(expectedPrice, balance);
        
        // Now Let's try to hack it with Settle Channel
        var msg = new RelayMessage
        {
            nexus = "simnet",
            index = 0,
            timestamp = simulator.CurrentTime,
            sender = relayAddress,
            receiver = user2.Address,
            script = new byte[] { 1, 2, 3, 4 }
        };
        
        var receipt = RelayReceipt.FromMessage(msg, user);
        
        var balanceBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, user2.Address);
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Relay, nameof(RelayContract.SettleChannel), receipt)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
        
        var balanceAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, user2.Address);
        
        Assert.Equal(balanceBefore, balanceAfter);


    }
    
    
}
