using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Storage;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;

using Xunit;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Tests.Blockchain.Contracts.Legacy;

[Collection(nameof(SystemTestCollectionDefinition))]
public class StorageContractTest
{
    public const string testAvatarData = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAAK3RFWHRDcmVhdGlvbiBUaW1lAHF1YSA3IGRleiAyMDIyIDEzOjM5OjAzIC0wMDAwejAqCAAAAAd0SU1FB+YMBw0nFwUgI3YAAAAJcEhZcwAACxIAAAsSAdLdfvwAAAAEZ0FNQQAAsY8L/GEFAAAC+0lEQVR42u1WS0xTQRS98960j4+CIOIniJEYPws+tYqNRo1bF240GEiLcWc0GmJgI4kaCYkUFlgkYeNCixJ1YYy60J0h8jEpEEQICsS2ii1IoZTWft68cWhDqfS1kNQuTLiZxXszZ+49d2buB1FKIZmCw19OgLZeiyRJPJUS0UgRx0hfP7pbGfxFIQ/0nSMin2b/9osSLjHGi+R4XpG9XUjBgZrj+xYN6N91WW0YkAJBQtxXiB88p1U7OZD88ygXOP7fameihLTRecx1z4qzdjdKzk1bx+1c95gLU39S1AeFs4xNUkjwYuMJRgjFWjOUl0iY3RVfY+yXvSE/p2g+X6TgRSC4qkMeE4M7Ind16vqhqXMvhroscwadKhoiCEJbeVHTsK3s1dAbu7dBp5I9CV5z9lJ0MBt0h1uGHe+tdrPTOzq9MOn2F2almh0+BMvI2vKSB31TPQ7HhMP3xTY/N+2+eebA28Gfa/LAA2Cy22e8hCIYm/E4/FKZJl+g7khMDpIm5pxml4t9jztcL7+zUFrzEWGJHsnNytmgFIHPy+AEoA3PewJIiMQ4gNuxdVM+n8aeYMHm1GPbcrzimo/o9efpulMFP9yBLelCYSauLc17NuBcgekdmbp1cpd54XeWwO3JzrhzoqC63QRRLwZVPe4nhKycBclDcXOlKp3CB8vs086vFPjoYBQJras4lKlkLw2q2wcxDcg8F1kDEZYIUw1xhbGJE0mrhNiq2oOYeEqSGMPrBtYN/LcGsLSKSUx9cVZ5SgjCbCwbCMYht7Q5oL+guq1Ts8no8sQm9+fnNmk1KR6bbPGigBsrS/Xa4kwcCCcuREngcscnLEn1OvXGJSjLTdWPPhK0nCcYqdaKvcClhlZFAjVPTJHaiehrvahZ4ipZCdcYBLDGi6U65gejD38lWypeNQ6EKjZLefe0ByHC8ZBcM5rCzrXIlVUIt47yQsUrD/s4zBu0atnOYAHghtHEnLuvLY7VOqD43TUJDmVsQGgzig1AyW7f/wCFjkiabQoUegAAAABJRU5ErkJggg==";

    private Address sysAddress;
    private PhantasmaKeys user;
    private PhantasmaKeys user2;
    private PhantasmaKeys owner;
    private Nexus nexus;
    private NexusSimulator simulator;
    private int amountRequested;
    private int gas;
    private BigInteger initialAmount;
    private BigInteger initialFuel;
    private BigInteger startBalance;
    private StakeReward reward;

    public StorageContractTest()
    {
        Initialize();
    }

    private void Initialize()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Friends);
        user = PhantasmaKeys.Generate();
        user2 = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(2000, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(1000, DomainSettings.FuelTokenDecimals);
        reward = new StakeReward(user.Address, Timestamp.Now);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }

    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(owner);
        nexus = simulator.Nexus;
        nexus.SetOracleReader(new OracleSimulator(nexus));
        simulator.GetFundsInTheFuture(owner);
        simulator.GetFundsInTheFuture(owner);
        SetInitialBalance(user.Address);
        SetInitialBalance(user2.Address);
    }

    protected void SetInitialBalance(Address address)
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, initialAmount);
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
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
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

    [Fact]
    public void AvatarUpload()
    {
        BigInteger stakeAmount =  UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);
        var filename = "avatar";
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), user.Address, stakeAmount)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var bytes = Encoding.UTF8.GetBytes(testAvatarData);
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
    }

    [Fact]
    public void TestPermissions()
    {
        BigInteger stakeAmount =  UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals);
        var filename = "avatar";
        
        // Stake to have storage space
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), user.Address, stakeAmount)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // Test upload
        var bytes = Encoding.UTF8.GetBytes(testAvatarData);
        var avatarHash = UploadArchive(user, filename, bytes, false);
        Assert.False(avatarHash.IsNull);

        var avatarArchive = simulator.Nexus.GetArchive(simulator.Nexus.RootStorage, avatarHash);
        Assert.NotNull(avatarArchive);
        Assert.True(avatarArchive.Name == filename);
        Assert.True(avatarArchive.Size == bytes.Length);
        Assert.True(avatarArchive.BlockCount == 1);
        
        var hasPermission = simulator.InvokeContract(NativeContractKind.Storage, nameof(StorageContract.HasPermission), user.Address, user.Address).AsBool();
        Assert.True(hasPermission);
        
        var hasPermissionUser2 = simulator.InvokeContract(NativeContractKind.Storage, nameof(StorageContract.HasPermission), user2.Address, user.Address).AsBool();
        Assert.False(hasPermissionUser2);
        
        // Give Permission
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Storage, nameof(StorageContract.AddPermission), user.Address, user2.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        hasPermissionUser2 = simulator.InvokeContract(NativeContractKind.Storage, nameof(StorageContract.HasPermission), user2.Address, user.Address).AsBool();
        Assert.True(hasPermissionUser2);
        
        // Remove Permission
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Storage, nameof(StorageContract.DeletePermission), user.Address, user2.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        hasPermissionUser2 = simulator.InvokeContract(NativeContractKind.Storage, nameof(StorageContract.HasPermission), user2.Address, user.Address).AsBool();
        Assert.False(hasPermissionUser2);
        
        // Re add Permission
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Storage, nameof(StorageContract.AddPermission), user.Address, user2.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // Migrate to new address
        var newAddress = PhantasmaKeys.Generate();
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Storage, nameof(StorageContract.MigratePermission), user.Address, user.Address, newAddress.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Migrate again
        var newAddress2 = PhantasmaKeys.Generate();
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Storage, nameof(StorageContract.MigratePermission), user.Address, user2.Address, newAddress2.Address)
                .SpendGas(user2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }

    [Fact]
    public void TestFiles()
    {
        BigInteger stakeAmount =  UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals);
        var filename = "avatar";
        
        // Stake to have storage space
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), user.Address, stakeAmount)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // Test upload
        var bytes = Encoding.UTF8.GetBytes(testAvatarData);
        var avatarHash = UploadArchive(user, filename, bytes, false);
        Assert.False(avatarHash.IsNull);

        var avatarArchive = simulator.Nexus.GetArchive(simulator.Nexus.RootStorage, avatarHash);
        Assert.NotNull(avatarArchive);
        Assert.True(avatarArchive.Name == filename);
        Assert.True(avatarArchive.Size == bytes.Length);
        Assert.True(avatarArchive.BlockCount == 1);

        // Check files
        var files = simulator.InvokeContract(NativeContractKind.Storage, nameof(StorageContract.GetFiles), user.Address).ToArray<Hash>();
        Assert.True(files.Length == 1);

        var hasFile = simulator.InvokeContract(NativeContractKind.Storage, nameof(StorageContract.HasFile), user.Address, files.First()).AsBool();
        Assert.True(hasFile);
        
        // Add permission
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Storage, nameof(StorageContract.AddPermission), user.Address, user2.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Add File
        var filename2 = "avatar2";
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Storage, nameof(StorageContract.AddFile), user2.Address, user.Address, avatarHash)
                .SpendGas(user2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        files = simulator.InvokeContract(NativeContractKind.Storage, nameof(StorageContract.GetFiles), user.Address).ToArray<Hash>();
        Assert.True(files.Length == 2);

        hasFile = simulator.InvokeContract(NativeContractKind.Storage, nameof(StorageContract.HasFile), user.Address, files.Last()).AsBool();
        Assert.True(hasFile);
    }

    public void TestStorageExploit()
    {
        // Try to upload file from another user to a different address
        // Give Permission 
        // Migrate
        // Migrate Permissions
        // And manipulate it.
    }
}
