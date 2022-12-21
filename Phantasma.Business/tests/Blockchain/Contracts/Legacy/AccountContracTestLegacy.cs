using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Storage;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Xunit;
using Shouldly;

namespace Phantasma.Business.Tests.Blockchain.Contracts.Legacy;

[Collection(nameof(SystemTestCollectionDefinition))]
public class AccountContracTestLegacy
{
    
    private static readonly string TEST_CONTRACT_PVM = "00040103010D00040941646472657373282907000401081A00000B00040103010D00040941646472657373282907000401040203020D00040941646472657373282907000402084A00000B00040103010D000409416464726573732829070004010201020D0302220000000000000000000000000000000000000000000000000000000000000000000003030D00040941646472657373282907000403190203040A04B1000D0204056572726F720C02000D030416496E73696465204F6E5769746E6573732066726F6D200201040E0404042303040503050D02040B52756E74696D652E4C6F670702000B";
    private static readonly string TEST_CONTRACT_ABI = "03096F6E557067726164650000000000010466726F6D08096F6E4D696772617465001B000000020466726F6D0802746F08096F6E5769746E657373004B000000010466726F6D0800";
    
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

    public AccountContracTestLegacy()
    {
        Initialize();
    }

    public void Initialize()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Account);
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
        simulator.LastBlockWasSuccessful().ShouldBeTrue();
    }
    
    public void AccountTriggersAllowance()
    {
        string[] scriptString;
        
        var target = PhantasmaKeys.Generate();
        var other = PhantasmaKeys.Generate();

        var symbol = "SOUL";
        var addressStr = Base16.Encode(other.Address.ToByteArray());

        scriptString = new string[]
        {
            $"alias r1, $temp",
            $"alias r2, $from",
            $"alias r3, $to",
            $"alias r4, $symbol",
            $"alias r5, $amount",

            $"jmp @end",

            $"@OnReceive: nop",
            $"pop $from",
            $"pop $to",
            $"pop $symbol",
            $"pop $amount",

            $@"load r1 ""{symbol}""",
            $@"equal r1, $symbol, $temp",
            $"jmpnot $temp, @end",

            $"load $temp 2",
            $"div $amount $temp $temp",

            $"push $temp",
            $"push $symbol",
            $"load r11 0x{addressStr}",
            $"push r11",
            $@"extcall ""Address()""",
            $"push $to",

            $"load r0 \"Runtime.TransferTokens\"",
            $"extcall r0",
            $"jmp @end",

            $"@end: ret"
        };


        Dictionary<string, int> labels;
        DebugInfo debugInfo;
        var script = AssemblerUtils.BuildScript(scriptString, "test", out debugInfo, out labels);

        var methods = TokenUtils.GetTriggersForABI(labels);
        var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, "KCAL", UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, "SOUL", UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals));
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(target, ProofOfWork.None,
            () => ScriptUtils.BeginScript()
                    .AllowGas(target.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), target.Address, UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals))
                    .SpendGas(target.Address)
                .EndScript());
        simulator.EndBlock();


        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(target, ProofOfWork.None,
            () => ScriptUtils.BeginScript()
                    .AllowGas(target.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterScript), target.Address, script, abi.ToByteArray())
                    .SpendGas(target.Address)
                .EndScript());
        simulator.EndBlock();

        var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, target.Address);

        var amount = UnitConversion.ToBigInteger(5, DomainSettings.StakingTokenDecimals);

        simulator.BeginBlock();
        var tx = simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, symbol, amount * 2);
        simulator.EndBlock();

        var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, target.Address);
        var expectedBalance = initialBalance + amount;
        Assert.True(balance == expectedBalance);

        balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, other.Address);
        Assert.True(balance == amount);
    }

    [Fact(Skip = "AccountTriggers")]
    public void AccountTriggersEventPropagation()
    {
        string[] scriptString;

        var owner = PhantasmaKeys.Generate();
        var target = PhantasmaKeys.Generate();
        var symbol = "TEST";
        var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        string message = "customEvent";
        var addressStr = Base16.Encode(target.Address.ToByteArray());
        var isTrue = true;

        scriptString = new string[]
        {
            $"alias r1, $triggerSend",
            $"alias r2, $triggerReceive",
            $"alias r3, $triggerBurn",
            $"alias r4, $triggerMint",
            $"alias r5, $currentTrigger",
            $"alias r6, $comparisonResult",
            $"alias r7, $triggerWitness",
            $"alias r8, $currentAddress",
            $"alias r9, $sourceAddress",

            $@"load $triggerSend, ""{AccountTrigger.OnSend}""",
            $@"load $triggerReceive, ""{AccountTrigger.OnReceive}""",
            $@"load $triggerBurn, ""{AccountTrigger.OnBurn}""",
            $@"load $triggerMint, ""{AccountTrigger.OnMint}""",
            $@"load $triggerWitness, ""{AccountTrigger.OnWitness}""",
            $"pop $currentTrigger",
            $"pop $currentAddress",

            $"equal $triggerWitness, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @witnessHandler",

            $"equal $triggerSend, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @sendHandler",

            $"equal $triggerReceive, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @receiveHandler",

            $"equal $triggerBurn, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @burnHandler",

            $"equal $triggerMint, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @OnMint",

            $"jmp @end",

            $"@witnessHandler: ",
            $"load r11 0x{addressStr}",
            $"push r11",
            "extcall \"Address()\"",
            $"pop $sourceAddress",
            $"equal $sourceAddress, $currentAddress, $comparisonResult",
            "jmpif $comparisonResult, @endWitness",
            $"load r1 \"test witness handler xception\"",
            $"throw r1",
            
            "jmp @end",

            $"@sendHandler: jmp @end",

            $"@receiveHandler: jmp @end",

            $"@burnHandler: jmp @end",

            $"@OnMint: load r11 0x{addressStr}",
            $"push r11",
            $@"extcall ""Address()""",
            $"pop r11",

            $"load r10, {(int)EventKind.Custom}",
            $@"load r12, ""{message}""",

            $"push r12",
            $"push r11",
            $"push r10",
            $@"extcall ""Runtime.Notify""",

            $"@endWitness: ret",
            $"load r11 {isTrue}",
            $"push r11",

            $"@end: ret"
        };

        var script = AssemblerUtils.BuildScript(scriptString, null, out var something, out var labels);
        var methods = new List<ContractMethod>();
        methods.Add(new ContractMethod("OnMint", VMType.None, 205, new ContractParameter[0]));
        var abi = new ContractInterface(methods, new List<ContractEvent>());

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, "KCAL", 60000000000000);
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, "SOUL", UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)*50000);
        simulator.GenerateCustomTransaction(target, ProofOfWork.None,
            () => ScriptUtils.BeginScript().AllowGas(target.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), target.Address, UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)*50000)
                .CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterScript), target.Address, script, abi.ToByteArray()).SpendGas(target.Address)
                .EndScript());
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.GenerateToken(target, symbol, $"{symbol}Token", 1000000000, 3, flags);
        var tx = simulator.MintTokens(target, target.Address, symbol, 1000);
        simulator.EndBlock();

        var accountScript = simulator.Nexus.LookUpAddressScript(simulator.Nexus.RootStorage, target.Address, simulator.CurrentTime);
        Assert.True(accountScript != null && accountScript.Length > 0);

        var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, symbol);
        var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, target.Address);
        Assert.True(balance == 1000);

        var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
        Assert.True(events.Count(x => x.Kind == EventKind.Custom) == 1);

        var eventData = events.First(x => x.Kind == EventKind.Custom).Data;
        var eventMessage = (VMObject)Serialization.Unserialize(eventData, typeof(VMObject));

        Assert.True(eventMessage.AsString() == message);
        Assert.Throws<ChainException>(() =>
        {
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, symbol, 10);
            simulator.EndBlock();
        });

        balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, target.Address);
        Assert.True(balance == 1000);
    }
    
    [Fact]
    public void AccountRegister()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var fuelSymbol = DomainSettings.FuelTokenSymbol;

        Func<PhantasmaKeys, string, bool> registerName = (keypair, name) =>
        {
            bool result = true;

            try
            {
                simulator.BeginBlock();
                var tx = simulator.GenerateAccountRegistration(keypair, name);
                var lastBlock = simulator.EndBlock().FirstOrDefault();

                if (lastBlock != null)
                {
                    Assert.True(tx != null);

                    var evts = lastBlock.GetEventsForTransaction(tx.Hash);
                    Assert.True(evts.Any(x => x.Kind == EventKind.AddressRegister));
                }
            }
            catch (Exception)
            {
                result = false;
            }

            return result;
        };

        var testUser = PhantasmaKeys.Generate();

        var token = nexus.GetTokenInfo(nexus.RootStorage, fuelSymbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);

        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);

        var initialBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.StakingTokenSymbol, owner.Address);
        Assert.True(initialBalance >= stakeAmount);

        // Send from Genesis address to test user
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, fuelSymbol, amount);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();

        // verify test user balance
        var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.True(balance == amount);

        // make user stake enough to register a name
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().
                AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit).
                CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, stakeAmount).
                SpendGas(testUser.Address).
                EndScript());
        simulator.EndBlock();

        var targetName = "hello";
        Assert.False(registerName(testUser, targetName.Substring(3)));
        Assert.False(registerName(testUser, targetName.ToUpper()));
        Assert.False(registerName(testUser, targetName + "!"));
        Assert.True(registerName(testUser, targetName));

        var currentName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, testUser.Address, simulator.CurrentTime);
        Assert.True(currentName == targetName);

        var someAddress = nexus.LookUpName(nexus.RootStorage, targetName, simulator.CurrentTime);
        Assert.True(someAddress == testUser.Address);

        Assert.False(registerName(testUser, "other"));
    }

    [Fact]
    public void AccountMigrate()
    {
        var symbol = DomainSettings.FuelTokenSymbol;

        var testUser = PhantasmaKeys.Generate();

        var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);

        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);

        // Send from Genesis address to test user
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();

        // verify test user balance
        var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.True(balance == amount);

        // make user stake enough to register a name
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().
                AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, stakeAmount).
                SpendGas(testUser.Address).
                EndScript());
        simulator.EndBlock();

        var targetName = "hello";

        simulator.BeginBlock();
        var tx = simulator.GenerateAccountRegistration(testUser, targetName);
        var lastBlock = simulator.EndBlock().FirstOrDefault();

        if (lastBlock != null)
        {
            Assert.True(tx != null);

            var evts = lastBlock.GetEventsForTransaction(tx.Hash);
            Assert.True(evts.Any(x => x.Kind == EventKind.AddressRegister));
        }


        var currentName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, testUser.Address, simulator.CurrentTime);
        Assert.True(currentName == targetName);

        var someAddress = nexus.LookUpName(nexus.RootStorage, targetName, simulator.CurrentTime);
        Assert.True(someAddress == testUser.Address);

        var migratedUser = PhantasmaKeys.Generate();

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
        {
            return ScriptUtils.BeginScript().
                 AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                 CallContract(NativeContractKind.Account, nameof(AccountContract.Migrate), testUser.Address, migratedUser.Address).
                 SpendGas(testUser.Address).
                 EndScript();
        });
        simulator.EndBlock().FirstOrDefault();

        currentName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, testUser.Address, simulator.CurrentTime);
        Assert.False(currentName == targetName);

        var newName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, migratedUser.Address, simulator.CurrentTime);
        Assert.True(newName == targetName);
    }

    [Fact]
    public void TestAccountRegister()
    {
        var testPVM = Base16.Decode(TEST_CONTRACT_PVM);
        var testABI = Base16.Decode(TEST_CONTRACT_ABI);
        
        // Test LookupName
        var lookUpName = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.LookUpName), "null").AsAddress();
        Assert.True(lookUpName == Address.Null);
        
        lookUpName = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.LookUpName), "test").AsAddress();
        Assert.True(lookUpName == Address.Null);

        // Test HasScript
        var hasScript = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.HasScript), user.Address).AsBool();
        Assert.False(hasScript);

        var accountAddress = SmartContract.GetAddressForNative(NativeContractKind.Account);
        hasScript = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.HasScript), accountAddress).AsBool();
        Assert.False(hasScript);
        
        // Lookup Script
        var scriptPVM = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.LookUpScript), user.Address);
        Assert.Equal(new byte[0], scriptPVM.AsByteArray());

        // LookUpABI
        var scriptABI = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.LookUpABI), user.Address);
        Assert.NotEqual(Encoding.UTF8.GetString(testABI), scriptABI.AsString());
        Assert.Equal(new byte[0], scriptPVM.AsByteArray());
        
        // Stake
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), user.Address, UnitConversion.ToBigInteger(5, DomainSettings.StakingTokenDecimals))
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Add Script
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterScript), user.Address, testPVM, testABI)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        hasScript = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.HasScript), user.Address).AsBool();
        Assert.True(hasScript);
        
        // Lookup script
        scriptPVM = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.LookUpScript), user.Address);
        Assert.False(scriptPVM.IsEmpty);
        Assert.True(scriptPVM.AsByteArray().SequenceEqual(testPVM));
        
        // Lookup ABI
        scriptABI = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.LookUpABI), user.Address);
        Assert.True(scriptABI.Size > 0);
        Assert.Equal(Encoding.UTF8.GetString(testABI), scriptABI.AsString());

        // GetTriggerForABI
        var result = AccountContract.GetTriggerForABI(AccountTrigger.OnWitness);
        Assert.Equal(result.name, "OnWitness");
        Assert.Equal(result.parameters.Length, 1);
        Assert.Equal(result.parameters[0].name, "from");
        Assert.Equal(result.parameters[0].type, VMType.Object);
    }


    [Fact]
    public void TestAccountMigrateWithStorageValidatorAndNFT()
    {
        var newUser = PhantasmaKeys.Generate();
        
        // Stake
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), user.Address, UnitConversion.ToBigInteger(5, DomainSettings.StakingTokenDecimals))
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Register name
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterName), user.Address, "test")
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        var lookUpName = simulator.InvokeContract(NativeContractKind.Account, nameof(AccountContract.LookUpName), "test").AsAddress();
        Assert.Equal(lookUpName, user.Address);

        // Create NFT
        var nftSymbol = "NFT";
        
        // TODO: Create NFT
        
        // Add Something to the storage
        var filename = "avatar";
        
        var bytes = Encoding.UTF8.GetBytes(StorageContractTest.testAvatarData);
        var avatarHash = UploadArchive(user, filename, bytes, false);
        Assert.False(avatarHash.IsNull);

        var avatarArchive = simulator.Nexus.GetArchive(simulator.Nexus.RootStorage, avatarHash);
        Assert.NotNull(avatarArchive);
        Assert.True(avatarArchive.Name == filename);
        Assert.True(avatarArchive.Size == bytes.Length);
        Assert.True(avatarArchive.BlockCount == 1);

        var chunk = simulator.Nexus.ReadArchiveBlock(avatarArchive, 0);
        Assert.NotNull(chunk);
        Assert.True(chunk.Length == bytes.Length);
        Assert.True(ByteArrayUtils.CompareBytes(chunk, bytes));
        
        // Register Script
        var testPVM = Base16.Decode(TEST_CONTRACT_PVM);
        var testABI = Base16.Decode(TEST_CONTRACT_ABI);
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterScript), user.Address, testPVM, testABI)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Migrate account with storage validator
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () => 
            ScriptUtils.BeginScript().
                AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                CallContract(NativeContractKind.Account, nameof(AccountContract.Migrate), user.Address, newUser.Address).
                SpendGas(user.Address).
                EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
    private Hash UploadArchive(PhantasmaKeys keys, string fileName, byte[] content, bool encrypt)
    {
        var target = keys.Address;

        var newFileName = Path.GetFileName(fileName);

        byte[] archiveEncryption;

        if (encrypt)
        {
            var privateEncryption = new PrivateArchiveEncryption(target);

            newFileName = privateEncryption.EncryptName(newFileName, keys);

            content = privateEncryption.Encrypt(content, keys);

            archiveEncryption = privateEncryption.ToBytes();
        }
        else
        {
            archiveEncryption = ArchiveExtensions.Uncompressed;
        }

        var fileSize = content.Length;

        var merkleTree = new MerkleTree(content);
        var merkleBytes = merkleTree.ToByteArray();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
        ScriptUtils.BeginScript()
        .AllowGas(target, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
            .CallContract(NativeContractKind.Storage, nameof(StorageContract.CreateFile), target, newFileName, fileSize, merkleBytes, archiveEncryption)
            .SpendGas(target)
            .EndScript());
        var uploadBlock = simulator.EndBlock().FirstOrDefault();
        Assert.True(simulator.LastBlockWasSuccessful());

        
        var totalChunks = MerkleTree.GetChunkCountForSize((uint)content.Length);

        for (int i=0; i<totalChunks; i++)
        {
            UploadChunk(fileName, merkleTree, content, uploadBlock.Hash, i, totalChunks);
        }

        return merkleTree.Root;
    }

    private void UploadChunk(string fileName, MerkleTree merkleTree, byte[] content, Hash creationTxHash, int blockIndex, uint _totalUploadChunks)
    {
        var lastChunk = _totalUploadChunks - 1;

        var isLast = blockIndex == lastChunk;

        var chunkSize = isLast ? content.Length % MerkleTree.ChunkSize : MerkleTree.ChunkSize;
        var chunkData = new byte[chunkSize];

        var offset = blockIndex * MerkleTree.ChunkSize;
        for (int i = 0; i < chunkSize; i++)
        {
            chunkData[i] = content[i + offset];
        }

        simulator.WriteArchive(merkleTree.Root, blockIndex, chunkData);
    }
}
