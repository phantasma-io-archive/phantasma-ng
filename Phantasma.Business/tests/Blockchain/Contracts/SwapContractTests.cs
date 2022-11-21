using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.RocksDB;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

public class SwapContractTests : IDisposable
{

    #region Mocking
    protected PhantasmaKeys Validator { get; set; }
    protected PhantasmaKeys TokenOwner { get; set; }
    protected PhantasmaKeys User1 { get; set; }
    protected PhantasmaKeys User2 { get; set; }
    protected string PartitionPath { get; set; }
    protected IToken FungibleToken { get; set; }
    protected IToken NonFungibleToken { get; set; }
    protected IToken NonTransferableToken { get; set; }
    protected IChain Chain { get; set; }
    protected Dictionary<string, BigInteger> Mints { get; set; }
     /*
    protected IRuntime CreateRuntime_MockedChain()
    {
        var tx = new Transaction(
                "mainnet",
                DomainSettings.RootChainName,
                new byte[1] { 0 },
                User1.Address,
                User1.Address,
                10000,
                999,
                Timestamp.Now + TimeSpan.FromDays(300),
                "UnitTest");

        tx.Sign(User1);
        tx.Sign(User2);
        tx.Sign(TokenOwner);

        return CreateRuntime(false, FungibleToken, null, tx, true, false);
    }

    protected IRuntime CreateRuntime_Default(bool delayPayment = false, Transaction tx = null, bool tokenExists = true)
    {
        if (tx == null)
        {
            tx = new Transaction(
                "mainnet",
                DomainSettings.RootChainName,
                new byte[1] { 0 },
                User1.Address,
                User1.Address,
                10000,
                999,
                Timestamp.Now + TimeSpan.FromDays(300),
                "UnitTest");

            tx.Sign(User1);
            tx.Sign(User2);
            tx.Sign(TokenOwner);
        }

        return CreateRuntime(delayPayment, FungibleToken, null, tx, false, tokenExists);
    }

    protected IRuntime CreateRuntime(
            bool delayPayment,
            IToken token,
            ExecutionContext context = null,
            Transaction tx = null,
            bool mockChain = false,
            bool tokenExists = true)
    {
        var contract = (SmartContract)Activator.CreateInstance<AccountContract>();
        var nexusMoq = new Mock<INexus>();

        // setup TransferTokens
        nexusMoq.Setup( n => n.TransferTokens(
                    It.IsAny<IRuntime>(),
                    It.IsAny<IToken>(),
                    It.IsAny<Address>(),
                    It.IsAny<Address>(),
                    It.IsAny<BigInteger>(),
                    It.IsAny<bool>())
                );

        nexusMoq.Setup( n => n.MintTokens(
                    It.IsAny<IRuntime>(),
                    It.IsAny<IToken>(),
                    It.IsAny<Address>(),
                    It.IsAny<Address>(),
                    It.IsAny<string>(),
                    It.IsAny<BigInteger>())
                )
            .Callback<IRuntime, IToken, Address, Address, string, BigInteger>(
                    (runtime, token, from, to, chain, amount) =>
                    {
                        this.Mints.Add(token.Symbol, amount);
                    });

        nexusMoq.Setup( n => n.HasGenesis).Returns(true);
        nexusMoq.Setup( n => n.MaxGas).Returns(100000);

        nexusMoq.Setup( n => n.GetStakeFromAddress(
                    It.IsAny<StorageContext>(),
                    It.IsAny<Address>()))
            .Returns(UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals));

        nexusMoq.Setup( n => n.GetTokenInfo(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns(token);

        nexusMoq.Setup( n => n.TokenExists(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns(tokenExists);

        nexusMoq.Setup( n => n.GetChainByName(
                    It.IsAny<string>())
                ).Returns((string name) => 
                    {
                        if (string.IsNullOrEmpty(name))
                        {
                            return null;
                        }
                        else
                        {
                            return this.Chain;
                        }
                    });

        nexusMoq.Setup( n => n.GetParentChainByName(
                    It.IsAny<string>())
                ).Returns<IChain>(null);

        nexusMoq.Setup( n => n.GetProtocolVersion(
                    It.IsAny<StorageContext>())
                ).Returns(0);

        nexusMoq.Setup( n => n.CreateOrganization(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>()));

        if (!mockChain)
        {
            this.Chain = new Chain(nexusMoq.Object, "main");
        }
        else
        {
            var chainMoq = new Mock<IChain>();
            chainMoq.Setup( c => c.GetTransactionHashesForAddress(
                        It.IsAny<Address>())
                    ).Returns(new Hash[]
                        { 
                            Hash.FromString("0"),
                            Hash.FromString("1"),
                            Hash.FromString("2"),
                            Hash.FromString("3"),
                            Hash.FromString("4"),
                        });

            chainMoq.Setup( c => c.Nexus
                    ).Returns(nexusMoq.Object);

            chainMoq.Setup( c => c.GetContractByName(It.IsAny<StorageContext>(), It.IsAny<string>())
                    ).Returns(contract);

            chainMoq.Setup( c => c.GetContractContext(It.IsAny<StorageContext>(), It.IsAny<SmartContract>())
                    ).Returns(new ChainExecutionContext(contract));

            chainMoq.Setup( c => c.GenerateUID(It.IsAny<StorageContext>())).Returns(1200);

            chainMoq.Setup( c => c.GetNameFromAddress(It.IsAny<StorageContext>(), It.IsAny<Address>())
                    ).Returns( (StorageContext context, Address address) => 
                        {
                            return address.ToString();
                        });

            // set chain mock
            this.Chain = chainMoq.Object;
        }

        var runtime = new RuntimeVM(
                0,
                new byte[3] { (byte)Opcode.NOP, (byte)Opcode.NOP , (byte)Opcode.RET },
                0,
                this.Chain,
                this.Validator.Address,
                Timestamp.Now,
                tx,
                new StorageChangeSetContext(new KeyStoreStorage(new DBPartition(this.PartitionPath))),
                null,
                ChainTask.Null,
                delayPayment
                );


        if (context is not null)
        {
            runtime.RegisterContext(token.Symbol.ToLower(), context);
            runtime.SetCurrentContext(context);
        }

        runtime.SwitchContext(runtime.EntryContext, 0);

        return runtime;
    }*/
    
    public SwapContractTests()
    {
        this.Mints = new ();
        this.TokenOwner = PhantasmaKeys.Generate();
        this.User1 = PhantasmaKeys.Generate();
        this.User2 = PhantasmaKeys.Generate();
        this.Validator = PhantasmaKeys.Generate();
        
        this.PartitionPath = Path.Combine(Path.GetTempPath(), "PhantasmaUnitTest", $"{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(this.PartitionPath);

        var maxSupply = 10000000;

        var ftFlags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        this.FungibleToken = new TokenInfo("EXX", "Example Token", TokenOwner.Address, 0, 8, ftFlags, new byte[1] { 0 }, new ContractInterface());

        var nftFlags = TokenFlags.Burnable | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        this.NonFungibleToken = new TokenInfo("EXNFT", "Example NFT", TokenOwner.Address, 0, 0, nftFlags, new byte[1] { 0 }, new ContractInterface());

        var ntFlags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable | TokenFlags.Stakable;
        this.NonTransferableToken = new TokenInfo("EXNT", "Example Token non transferable", TokenOwner.Address, 0, 8, ntFlags, new byte[1] { 0 }, new ContractInterface());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.PartitionPath, true);
        }
        catch (IOException)
        {
            Console.WriteLine("Unable to clean test directory");
        }

        this.Mints.Clear();
    }
    #endregion
}
