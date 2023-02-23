using System;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Storage;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Storage;

[Collection(nameof(SystemTestCollectionDefinition))]
public class FileStorageTests
{
    public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

    
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

    public FileStorageTests()
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
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
    #region SuccessTests
    //stake soul and upload a file under the available space limit
    [Fact]
    public void SingleUploadSuccess()
    {
        var testUser = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 5;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call for minimum staking amount
        var stakeAmount = accountBalance / 2;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

        //-----------
        //Upload a file: should succeed
        var filename = "notAVirus.exe";
        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)((stakedAmount / MinimumValidStake * KilobytesPerStake) * 1024) - (long)headerSize;
        var content = new byte[contentSize];
        var rnd = new Random();
        for (int i=0; i<content.Length; i++)
        {
            content[i] = (byte)rnd.Next();
        }

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());

        //System.IO.File.WriteAllText(@"c:\code\bug_vm.txt", string.Join('\n', new VM.Disassembler(tx.Script).Instructions));
        simulator.EndBlock();

        var usedSpace = simulator.InvokeContract("storage", "GetUsedSpace", testUser.Address).AsNumber();
        Console.WriteLine($"{usedSpace} / {contentSize}");
        Assert.True(usedSpace == contentSize);

        Assert.True(simulator.Nexus.ArchiveExists(simulator.Nexus.RootStorage, contentMerkle.Root));
        var archive = simulator.Nexus.GetArchive(simulator.Nexus.RootStorage, contentMerkle.Root);

        //TODO not sure what that part is for...
        //for (int i=0; i<archive.BlockCount; i++)
        //{
        //    int ofs = (int)(i * archive.BlockSize);
        //    Console.WriteLine("ofs: " + ofs);
        //    var blockContent = content.Skip(ofs).Take((int)archive.BlockSize).ToArray();
        //    simulator.Nexus.WriteArchiveBlock(archive, i, blockContent);
        //}
    }

    [Fact]
    public void SingleUploadSuccessMaxFileSize()
    {
        var testUser = PhantasmaKeys.Generate();

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootStorage, StorageContract.KilobytesPerStakeTag);

        BigInteger accountBalance = (DomainSettings.ArchiveMaxSize / 1024) / KilobytesPerStake;  //provide enough account balance for max file size available space
        accountBalance *= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call
        var stakeAmount = accountBalance;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        //-----------
        //Upload a file: should succeed
        var filename = "notAVirus.exe";
        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(DomainSettings.ArchiveMaxSize) - (long)headerSize;
        var content = new byte[contentSize];
        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        //System.IO.File.WriteAllText(@"D:\Repos\bug_vm.txt", string.Join('\n', new VM.Disassembler(tx.Script).Instructions));
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == contentSize);
    }

    //upload a file for less than available space and perform partial unstake
    [Fact]
    public void ReduceAvailableSpace()
    {
        var testUser = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call for minimum staking amount
        var stakedAmount = MinimumValidStake * 5;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakedAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        //-----------
        //Upload a file
        var filename = "notAVirus.exe";

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootStorage, StorageContract.KilobytesPerStakeTag);

        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024 / 5) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == contentSize);

        //-----------
        //Time skip 1 day
        simulator.TimeSkipDays(1);

        //-----------
        //Try partial unstake: should succeed
        var initialStakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        var stakeReduction = stakedAmount / 5;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Unstake", testUser.Address, stakeReduction).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        var finalStakedAmount = simulator.InvokeContract(NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();

        Assert.True(finalStakedAmount == initialStakedAmount - stakeReduction);
    }

    //upload a file for full space, delete file and perform full unstake
    [Fact]
    public void UnstakeAfterUsedSpaceRelease()
    {
        var testUser = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call for minimum staking amount
        var stakedAmount = MinimumValidStake;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakedAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        //-----------
        //Upload a file
        var filename = "notAVirus.exe";

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootStorage, StorageContract.KilobytesPerStakeTag);

        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();
        var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
        var eventData = events.First(x => x.Kind == EventKind.FileCreate).Data;
        var archiveHash = new Hash(eventData.Skip(1).ToArray());

        var usedSpace = simulator.InvokeContract("storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == contentSize);

        //-----------
        //Delete the file

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "DeleteFile", testUser.Address, archiveHash).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        usedSpace = simulator.InvokeContract("storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == 0);

        //-----------
        //Time skip 1 day
        simulator.TimeSkipDays(1);

        //-----------
        //Try to unstake everything: should succeed
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Unstake", testUser.Address, stakedAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        var finalStakedAmount = simulator.InvokeContract(NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        Assert.True(finalStakedAmount == 0);

    }

    //upload more than one file for a total size that is less than the available space
    [Fact]
    public void CumulativeUploadSuccess()
    {
        var testUser = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call
        var stakeAmount = MinimumValidStake * 2;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.InvokeContract(NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootStorage, StorageContract.KilobytesPerStakeTag);

        //-----------
        //Upload a file: should succeed
        var filename = "notAVirus.exe";
        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 4) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        var usedSpace = simulator.InvokeContract("storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == contentSize);

        var oldSpace = contentSize;
        //----------
        //Upload another file: should succeed

        filename = "giftFromTroia.exe";
        headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 4) - (long)headerSize;
        content = new byte[contentSize];

        contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        usedSpace = simulator.InvokeContract("storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == oldSpace + contentSize);

        oldSpace += contentSize;
        //----------
        //Upload another file: should succeed

        filename = "JimTheEarthWORM.exe";
        headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 4) - (long)headerSize;
        content = new byte[contentSize];

        contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == oldSpace + contentSize);
    }

    //reupload a file maintaining the same name after deleting the original one
    [Fact]
    public void ReuploadSuccessAfterDelete()
    {
        var testUser = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call
        var stakeAmount = MinimumValidStake * 2;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

        //-----------
        //Upload a file: should succeed
        var filename = "notAVirus.exe";
        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 2) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();
        var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
        var eventData = events.First(x => x.Kind == EventKind.FileCreate).Data;
        var archiveHash = new Hash(eventData.Skip(1).ToArray());

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == contentSize);

        var oldSpace = contentSize;

        //-----------
        //Delete the file

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "DeleteFile", testUser.Address, archiveHash).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == 0);

        //----------
        //Upload the same file: should succeed
        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == oldSpace);
    }

    //upload a duplicate of an already uploaded file but by a different owner
    [Fact]
    public void UploadDuplicateFileDifferentOwner()
    {
        var testUserA = PhantasmaKeys.Generate();
        var testUserB = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call for userA
        var stakeAmount = MinimumValidStake * 2;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUserA.Address, stakeAmount).
                SpendGas(testUserA.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUserA.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        //----------
        //Perform a valid Stake call for userB
        startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUserB.Address, stakeAmount).
                SpendGas(testUserB.Address).EndScript());
        simulator.EndBlock();

        stakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUserB.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

        //-----------
        //User A uploads a file: should succeed
        var filename = "notAVirus.exe";
        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 2) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUserA.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUserA.Address).EndScript());
        simulator.EndBlock();

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUserA.Address).AsNumber();

        Assert.True(usedSpace == contentSize);

        //----------
        //User B uploads the same file: should succeed
        contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUserB.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUserB.Address).EndScript());
        simulator.EndBlock();

        usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUserB.Address).AsNumber();

        Assert.True(usedSpace == contentSize);
    }

    //upload a duplicate of an already uploaded file but by a different owner
    [Fact]
    public void UploadDuplicateFileSameOwner()
    {
        var testUserA = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call for userA
        var stakeAmount = MinimumValidStake * 2;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUserA.Address, stakeAmount).
                SpendGas(testUserA.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUserA.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

        //-----------
        //User A uploads a file: should succeed
        var filename = "notAVirus.exe";
        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 2) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUserA.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUserA.Address).EndScript());
        simulator.EndBlock();

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUserA.Address).AsNumber();

        Assert.True(usedSpace == contentSize);

        //----------
        //User B uploads the same file: should succeed
        contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUserA.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUserA.Address).EndScript());
        simulator.EndBlock();

        usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUserA.Address).AsNumber();

        Assert.True(usedSpace == contentSize);
    }
    #endregion

    #region FailureTests

    //try unstaking below required space for currently uploaded files
    [Fact]
    public void UnstakeWithStoredFilesFailure()
    {
        var testUser = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        
        //-----------
        //Perform a valid Stake call for minimum staking amount
        var stakedAmount = MinimumValidStake;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakedAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

        //-----------
        //Upload a file
        var filename = "notAVirus.exe";

        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == contentSize);

        var oldSpace = usedSpace;

        //-----------
        //Time skip 1 day
        simulator.TimeSkipDays(1);

        //-----------
        //Try to unstake everything: should fail due to files still existing for this user
        var initialStakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        var stakeReduction = initialStakedAmount - MinimumValidStake;
        startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Unstake", testUser.Address, stakeReduction).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        var finalStakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        Assert.True(initialStakedAmount == finalStakedAmount);

        usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == oldSpace);
    }

    //try to upload a single file beyond available space
    [Fact]
    public void UploadBeyondAvailableSpace()
    {
        var testUser = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call for minimum staking amount
        var stakeAmount = MinimumValidStake;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

        //-----------
        //Upload a file: should fail due to exceeding available space
        var filename = "notAVirus.exe";
        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize * 2, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();
        
        Assert.False(simulator.LastBlockWasSuccessful());

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == 0);
    }

    //try to upload multiple files that individually dont go above available space, but that cumulatively do so
    [Fact]
    public void CumulativeUploadMoreThanAvailable()
    {
        var testUser = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call for minimum staking amount
        var stakeAmount = MinimumValidStake;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

        //-----------
        //Upload a file: should succeed
        var filename = "notAVirus.exe";
        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == contentSize);

        var oldSpace = contentSize;
        //----------
        //Upload a file: should fail due to exceeding available storage capacity

        filename = "giftFromTroia.exe";
        headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
        content = new byte[contentSize];

            contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        Assert.True(usedSpace == oldSpace);
    }

    //upload a file with the same name as an already uploaded file
    [Fact]
    public void UploadDuplicateFilename()
    {
        var testUser = PhantasmaKeys.Generate();

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call
        var stakeAmount = MinimumValidStake * 2;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.InvokeContract( NativeContractKind.Stake, "GetStake", testUser.Address).AsNumber();
        Assert.True(stakedAmount == stakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(stakeAmount == startingSoulBalance - finalSoulBalance);

        var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

        //-----------
        //Upload a file: should succeed
        var filename = "notAVirus.exe";
        var headerSize = StorageContract.CalculateRequiredSize(filename, 0);
        var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 2) - (long)headerSize;
        var content = new byte[contentSize];

        var contentMerkle = new MerkleTree(content);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();

        Assert.True(usedSpace == contentSize );

        var oldSpace = contentSize;

        //----------
        //Upload a file with the same name: should fail
        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, content, ArchiveExtensions.Uncompressed).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();
        
        Assert.False(simulator.LastBlockWasSuccessful());


        Assert.True(usedSpace == oldSpace);
    }


    #endregion

    [Fact]
    public void SmallFileContractUpload()
    {
        var testUser = PhantasmaKeys.Generate();

        var stakeAmount = MinimumValidStake * 5;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(20,DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        var sb = new System.Text.StringBuilder();
        for (int i=0; i<64; i++)
        {
            sb.AppendLine("Hello Phantasma!");
        }

        var testMsg = sb.ToString();
        var textFile = System.Text.Encoding.UTF8.GetBytes(testMsg);
        var key = ArchiveExtensions.Uncompressed;
        var merkle = new MerkleTree(textFile);

        simulator.BeginBlock();
        var tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(testUser.Address, Address.Null,simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract("storage", "CreateFile", testUser.Address, "test.txt", textFile.Length, Serialization.Serialize(merkle), key).
              SpendGas(testUser.Address).
              EndScript()
        );
        var block = simulator.EndBlock().FirstOrDefault();
        Assert.True(simulator.LastBlockWasSuccessful());

        var usedSpace = simulator.InvokeContract( "storage", "GetUsedSpace", testUser.Address).AsNumber();
        Assert.Equal(usedSpace, textFile.Length);
    }
}