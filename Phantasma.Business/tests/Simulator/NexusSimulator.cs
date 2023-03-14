using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Core.Types;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Business.VM.Utils;
using Phantasma.Business.VM;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Storage.Context;
using Phantasma.Infrastructure.Pay.Chains;
using VMType = Phantasma.Core.Domain.VMType;

using Xunit;
using Phantasma.Node.Chains.Ethereum;
using Phantasma.Node.Chains.Neo2;
using Tendermint.Abci;
using Phantasma.Business.Blockchain.Contracts.Native;

namespace Phantasma.Business.Tests.Simulator;

public class SideChainPendingBlock
{
    public Hash hash;
    public Chain sourceChain;
    public Chain destChain;
    public string tokenSymbol;
}

public struct SimNFTData
{
    public byte A;
    public byte B;
    public byte C;
}

// TODO this should be moved to a better place, refactored or even just deleted if no longer useful
public class NexusSimulator
{
    public Nexus Nexus { get; private set; }
    public DateTime CurrentTime;

    private Random _rnd;
    private List<PhantasmaKeys> _keys = new List<PhantasmaKeys>();

    private PhantasmaKeys[] _validators;
    private PhantasmaKeys _currentValidator;

    public IEnumerable<Address> CurrentValidatorAddresses => _validators.Select(x => x.Address);

    public static BigInteger DefaultGasLimit = 100000;

    private Chain bankChain;
    
    public bool FailedTx { get; private set; }
    public string FailedTxReason { get; private set; }

    private static readonly string[] accountNames = {
        "aberration", "absence", "aceman", "acid", "alakazam", "alien", "alpha", "angel", "angler", "anomaly", "answer", "antsharer", "aqua", "archangel",
        "aspect", "atom", "avatar", "azure", "behemoth", "beta", "bishop", "bite", "blade", "blank", "blazer", "bliss", "boggle", "bolt",
        "bullet", "bullseye", "burn", "chaos", "charade", "charm", "chase", "chief", "chimera", "chronicle", "cipher", "claw", "cloud", "combo",
        "comet", "complex", "conjurer", "cowboy", "craze", "crotchet", "crow", "crypto", "cryptonic", "curse", "dagger", "dante", "daydream",
        "dexter", "diablo", "doctor", "doppelganger", "drake", "dread", "ecstasy", "enigma", "epitome", "essence", "eternity", "face",
        "fetish", "fiend", "flash", "fragment", "freak", "fury", "ghoul", "gloom", "gluttony", "grace", "griffin", "grim",
        "whiz", "wolf", "wrath", "zero", "zigzag", "zion"
    };

    public TimeSpan blockTimeSkip = TimeSpan.FromSeconds(10);
    public int MinimumFee => DomainSettings.DefaultMinimumGasFee;
    public BigInteger MinimumGasLimit => 100000;

    public NexusSimulator(PhantasmaKeys owner, int seed = 1234, Nexus nexus = null) : this(new PhantasmaKeys[] { owner }, seed, nexus, DomainSettings.LatestKnownProtocol)
    {
    }

    public NexusSimulator(PhantasmaKeys[] owners, int seed = 123, Nexus nexus = null) : this(owners, seed, nexus, DomainSettings.LatestKnownProtocol)
    {

    }

    public NexusSimulator(PhantasmaKeys[] owners, int protocolVersion): this(owners, 123, null, protocolVersion)
    {
        
    }

    public NexusSimulator(PhantasmaKeys[] owners, int seed, Nexus nexus, int protocolVersion)
    {
        _validators = owners;
        _currentValidator = owners[0];

        if (nexus == null)
        {
            nexus = new Nexus("simnet");
            nexus.SetOracleReader(new OracleSimulator(nexus));
        }

        this.Nexus = nexus;

        CurrentTime = Timestamp.Now; // (Timestamp) new DateTime(2018, 8, 26, 0, 0, 0, DateTimeKind.Utc)

        nexus.SetInitialValidators(CurrentValidatorAddresses);

        if (!Nexus.HasGenesis())
        {
            InitGenesis(protocolVersion);
        }
        else
        {
            var lastBlockHash = Nexus.RootChain.GetLastBlockHash();
            var lastBlock = Nexus.RootChain.GetBlockByHash(lastBlockHash);
            CurrentTime = new Timestamp(lastBlock.Timestamp.Value + 1);
            DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);
        }

        _rnd = new Random(seed);
        _keys.Add(_currentValidator);

        var oneFuel = UnitConversion.ToBigInteger(1, DomainSettings.FuelTokenDecimals);
        var token = Nexus.GetTokenInfo(Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        var localBalance = Nexus.RootChain.GetTokenBalance(Nexus.RootStorage, token, _currentValidator.Address);

        if (localBalance < oneFuel)
        {
            throw new Exception("Funds missing oops");
        }

        /*
        var market = Nexus.FindChainByName("market");
        var nftSales = new List<KeyValuePair<KeyPair, BigInteger>>();

        BeginBlock();
        for (int i = 1; i < 7; i++)
        {
            BigInteger ID = i + 100;
            TokenContent info;
            try
            {
                info = Nexus.GetNFT(nachoSymbol, ID);
            }
            catch  
            {
                continue;
            }

            var chain = Nexus.FindChainByAddress(info.CurrentChain);
            if (chain == null)
            {
                continue;
            }

            var nftOwner = chain.GetTokenOwner(nachoSymbol, ID);

            if (nftOwner == Address.Null)
            {
                continue;
            }

            foreach (var key in _keys)
            {
                if (key.Address == nftOwner)
                {
                    nftSales.Add(new KeyValuePair<KeyPair, BigInteger>(key, ID));
                    // send some gas to the sellers
                    GenerateTransfer(_owner, key.Address, Nexus.RootChain, Nexus.FuelTokenSymbol, UnitConversion.ToBigInteger(0.01m, Nexus.FuelTokenDecimals));
                }
            }
        }

        EndBlock();

        BeginBlock();
        foreach (var sale in nftSales)
        {
            // TODO this later should be the market chain instead of root
            GenerateNftSale(sale.Key, Nexus.RootChain, nachoSymbol, sale.Value, UnitConversion.ToBigInteger(100 + 5 * _rnd.Next() % 50, Nexus.FuelTokenDecimals));
        }
        EndBlock();
        */
    }

    public void SetValidator( PhantasmaKeys validator)
    {
        _currentValidator = validator;
        this.Nexus.RootChain.ValidatorKeys = _currentValidator;
    }

    public void GetFundsInTheFuture(PhantasmaKeys target, int times = 40)
    {
        for(int i = 0; i < times; i++)
            TimeSkipDays(90);

        foreach (var validator in _validators)
        {
            BeginBlock();
            GenerateCustomTransaction(validator, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript()
                        .AllowGas(validator.Address, Address.Null, MinimumFee, DefaultGasLimit)
                        .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), validator.Address, validator.Address)
                        .SpendGas(validator.Address)
                        .EndScript());
            var block = EndBlock();
        }
        
        TransferOwnerAssetsToAddress(target.Address);
    }

    public void TransferOwnerAssetsToAddress(Address target)
    {
        var rootChain = Nexus.RootChain;

        foreach (var validator in _validators)
        {
            if (validator.Address == target)
            {
                continue;
            }
            BeginBlock();


            var balance = rootChain.GetTokenBalance(Nexus.RootStorage, DomainSettings.StakingTokenSymbol, validator.Address);
            if (balance > 0)
            {
                GenerateTransfer(validator, target, rootChain, DomainSettings.StakingTokenSymbol, balance);
            }
            
            var balanceKCAL = rootChain.GetTokenBalance(Nexus.RootStorage, DomainSettings.StakingTokenSymbol, validator.Address);
            if (balanceKCAL > 0)
            {
                GenerateTransfer(validator, target, rootChain, DomainSettings.FuelTokenSymbol, balanceKCAL - UnitConversion.ToBigInteger(5, DomainSettings.FuelTokenDecimals));
            }
            EndBlock();

        }
    }

    private void InitGenesis(int protocolVersion)
    {
        var genesisTx = Nexus.CreateGenesisTransaction(CurrentTime, _currentValidator, protocolVersion);
        if (genesisTx == null)
        {
            throw new ChainException("Genesis block failure");
        }

        genesisTx.Sign(_currentValidator);

        // genesis block
        BeginBlock();
        AddTransactionToPendingBlock(genesisTx, Nexus.RootChain);
        EndBlock();

        var initialBalance = Nexus.RootChain.GetTokenBalance(Nexus.RootStorage, DomainSettings.StakingTokenSymbol, _currentValidator.Address);
        // check if the owner address got at least enough tokens to be a SM
        Assert.True(initialBalance >= StakeContract.DefaultMasterThreshold, FailedTxReason);

        /*
        var neoPlatform = NeoWallet.NeoPlatform;
        var neoKeys = InteropUtils.GenerateInteropKeys(owner, Nexus.GetGenesisHash(Nexus.RootStorage), neoPlatform);
        var neoText = NeoKeys.FromWIF(neoKeys.ToWIF()).Address;
        var neoAddress = NeoWallet.EncodeAddress(neoText);

        
        var ethPlatform = EthereumWallet.EthereumPlatform;
        var ethKeys = InteropUtils.GenerateInteropKeys(_owner, Nexus.GetGenesisHash(Nexus.RootStorage), ethPlatform);
        var ethText = EthereumKey.FromWIF(ethKeys.ToWIF()).Address;
        var ethAddress = EthereumWallet.EncodeAddress(ethText);

        var bscPlatform = BSCWallet.BSCPlatform;
        var bscKeys = InteropUtils.GenerateInteropKeys(_owner, Nexus.GetGenesisHash(Nexus.RootStorage), bscPlatform);
        var bscText = EthereumKey.FromWIF(bscKeys.ToWIF()).Address;
        var bscAddress = BSCWallet.EncodeAddress(bscText);*/

        /*
        BeginBlock();
        GenerateCustomTransaction(_owner, 0, () => new ScriptBuilder().AllowGas(_owner.Address, Address.Null, MinimumFee, DefaultGasLimit).
            CallInterop("Nexus.CreatePlatform", _owner.Address, neoPlatform, neoText, neoAddress, "GAS").
            CallInterop("Nexus.CreatePlatform", _owner.Address, ethPlatform, ethText, ethAddress, "ETH").
            CallInterop("Nexus.CreatePlatform", _owner.Address, bscPlatform, bscText, bscAddress, "BNB").
        SpendGas(_owner.Address).EndScript());

        var orgFunding = UnitConversion.ToBigInteger(1863626, DomainSettings.StakingTokenDecimals);
        var orgScript = new byte[0];
        var orgID = DomainSettings.PhantomForceOrganizationName;
        var orgAddress = Address.FromHash(orgID);

        GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
        {
            return new ScriptBuilder().AllowGas(_owner.Address, Address.Null, MinimumFee, DefaultGasLimit).
            CallInterop("Nexus.CreateOrganization", _owner.Address, orgID, "Phantom Force", orgScript).
            CallInterop("Organization.AddMember", _owner.Address, orgID, _owner.Address).
            TransferTokens(DomainSettings.StakingTokenSymbol, _owner.Address, orgAddress, orgFunding).
            CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), orgAddress, DomainSettings.StakingTokenSymbol, 500000).
            CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), orgAddress, orgFunding - (5000)).
            SpendGas(_owner.Address).
            EndScript();
        });
        EndBlock();*/

        /*
        BeginBlock();
        var communitySupply = 100000;
        GenerateToken(_owner, "MKNI", "Mankini Token", UnitConversion.ToBigInteger(communitySupply, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite);
        MintTokens(_owner, _owner.Address, "MKNI", communitySupply);
        EndBlock();

        BeginBlock();
        GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
        {
            return new ScriptBuilder().AllowGas(_owner.Address, Address.Null, MinimumFee, DefaultGasLimit).
            CallContract(NativeContractKind.Sale, nameof(SaleContract.CreateSale), _owner.Address, "Mankini sale", SaleFlags.None, (Timestamp)(this.CurrentTime + TimeSpan.FromHours(5)), (Timestamp)(this.CurrentTime + TimeSpan.FromDays(5)), "MKNI", DomainSettings.StakingTokenSymbol, 7, 0, 1000, 1, 100).
            SpendGas(_owner.Address).
            EndScript();
        });
        EndBlock();*/


        //TODO add SOUL/KCAL on ethereum, removed for now because hash is not fixed yet
        //BeginBlock();
        //GenerateCustomTransaction(_owner, ProofOfWork.Minimal, () =>
        //{
        //    return new ScriptBuilder().AllowGas(_owner.Address, Address.Null, MinimumFee, DefaultGasLimit).
        //    CallInterop("Nexus.SetTokenPlatformHash", "SOUL", ethPlatform, Hash.FromUnpaddedHex("53d5bdb2c8797218f8a0e11e997c4ab84f0b40ce")). // eth ropsten testnet hash
        //    CallInterop("Nexus.SetTokenPlatformHash", "KCAL", ethPlatform, Hash.FromUnpaddedHex("67B132A32E7A3c4Ba7dEbedeFf6290351483008f")). // eth ropsten testnet hash
        //    SpendGas(_owner.Address).
        //    EndScript();
        //});
        //EndBlock();
    }

    public void InitPlatforms()
    {
        var neoPlatform = NeoWallet.NeoPlatform;
        var neoKeys = InteropUtils.GenerateInteropKeys(_currentValidator, Nexus.GetGenesisHash(Nexus.RootStorage), neoPlatform);
        var neoText = NeoKeys.FromWIF(neoKeys.ToWIF()).Address;
        var neoAddress = NeoWallet.EncodeAddress(neoText);

        var ethPlatform = EthereumWallet.EthereumPlatform;
        var ethKeys = InteropUtils.GenerateInteropKeys(_currentValidator, Nexus.GetGenesisHash(Nexus.RootStorage), ethPlatform);
        var ethText = EthereumKey.FromWIF(ethKeys.ToWIF()).Address;
        var ethAddress = EthereumWallet.EncodeAddress(ethText);

        var bscPlatform = BSCWallet.BSCPlatform;
        var bscKeys = InteropUtils.GenerateInteropKeys(_currentValidator, Nexus.GetGenesisHash(Nexus.RootStorage), bscPlatform);
        var bscText = EthereumKey.FromWIF(bscKeys.ToWIF()).Address;
        var bscAddress = BSCWallet.EncodeAddress(bscText);

        Nexus.CreatePlatform(Nexus.RootStorage, neoText, neoAddress, neoPlatform, "GAS");
        Nexus.CreatePlatform(Nexus.RootStorage, ethText, ethAddress, ethPlatform, "ETH");
        Nexus.CreatePlatform(Nexus.RootStorage, bscText, bscAddress, bscPlatform, "BNB");

        
        /*BeginBlock();
        GenerateCustomTransaction(_currentValidator, ProofOfWork.None, () => new ScriptBuilder()
            .AllowGas(_currentValidator.Address, Address.Null, MinimumFee, DefaultGasLimit)
            .CallInterop("Nexus.CreatePlatform", _currentValidator.Address, neoPlatform, neoText, neoAddress, "GAS")
            .CallInterop("Nexus.CreatePlatform", _currentValidator.Address, ethPlatform, ethText, ethAddress, "ETH")
            .CallInterop("Nexus.CreatePlatform", _currentValidator.Address, bscPlatform, bscText, bscAddress, "BNB")
            .SpendGas(_currentValidator.Address)
            .EndScript());

        var orgFunding = UnitConversion.ToBigInteger(1863626, DomainSettings.StakingTokenDecimals);
        var orgScript = new byte[0];
        var orgID = DomainSettings.PhantomForceOrganizationName;
        var orgAddress = Address.FromHash(orgID);*/

        /*GenerateCustomTransaction(_currentValidator, ProofOfWork.None, () =>
        {
            return new ScriptBuilder().AllowGas(_currentValidator.Address, Address.Null, MinimumFee, DefaultGasLimit).
            CallInterop("Nexus.CreateOrganization", _currentValidator.Address, orgID, "Phantom Force", orgScript).
            CallInterop("Organization.AddMember", _currentValidator.Address, orgID, _currentValidator.Address).
            TransferTokens(DomainSettings.StakingTokenSymbol, _currentValidator.Address, orgAddress, orgFunding).
            CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), orgAddress, DomainSettings.StakingTokenSymbol, 500000).
            CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), orgAddress, orgFunding - (5000)).
            SpendGas(_currentValidator.Address).
            EndScript();
        });
        EndBlock();*/

        /*
        
        BeginBlock();
        var communitySupply = 100000;
        GenerateToken(_currentValidator, "MKNI", "Mankini Token", UnitConversion.ToBigInteger(communitySupply, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite);
        MintTokens(_currentValidator, _currentValidator.Address, "MKNI", communitySupply);
        EndBlock();

        BeginBlock();
        GenerateCustomTransaction(_currentValidator, ProofOfWork.None, () =>
        {
            return new ScriptBuilder().AllowGas(_currentValidator.Address, Address.Null, MinimumFee, DefaultGasLimit).
            CallContract(NativeContractKind.Sale, nameof(SaleContract.CreateSale), _currentValidator.Address, "Mankini sale", SaleFlags.None, (Timestamp)(this.CurrentTime + TimeSpan.FromHours(5)), (Timestamp)(this.CurrentTime + TimeSpan.FromDays(5)), "MKNI", DomainSettings.StakingTokenSymbol, 7, 0, 1000, 1, 100).
            SpendGas(_currentValidator.Address).
            EndScript();
        });
        EndBlock();*/
        
        //TODO add SOUL/KCAL on ethereum, removed for now because hash is not fixed yet
        //BeginBlock();
        //GenerateCustomTransaction(_owner, ProofOfWork.Minimal, () =>
        //{
        //    return new ScriptBuilder().AllowGas(_owner.Address, Address.Null, MinimumFee, DefaultGasLimit).
        //    CallInterop("Nexus.SetTokenPlatformHash", "SOUL", ethPlatform, Hash.FromUnpaddedHex("53d5bdb2c8797218f8a0e11e997c4ab84f0b40ce")). // eth ropsten testnet hash
        //    CallInterop("Nexus.SetTokenPlatformHash", "KCAL", ethPlatform, Hash.FromUnpaddedHex("67B132A32E7A3c4Ba7dEbedeFf6290351483008f")). // eth ropsten testnet hash
        //    SpendGas(_owner.Address).
        //    EndScript();
        //});
        //EndBlock();
    }

    private List<Transaction> transactions = new List<Transaction>();

    // there are more elegant ways of doing this...
    private Dictionary<Hash, Chain> txChainMap = new Dictionary<Hash, Chain>();
    private Dictionary<Hash, Transaction> txHashMap = new Dictionary<Hash, Transaction>();

    private HashSet<Address> pendingNames = new HashSet<Address>();

    private bool blockOpen = false;
    private PhantasmaKeys blockValidator;

    public void BeginBlock()
    {
        BeginBlock(_currentValidator);
    }

    public void BeginBlock(PhantasmaKeys validator)
    {
        if (blockOpen)
        {
            throw new Exception("Simulator block not terminated");
        }

        FailedTx = false;
        FailedTxReason = "";

        this.blockValidator = validator;

        _currentValidator = validator;

        transactions.Clear();
        txChainMap.Clear();
        txHashMap.Clear();

        var readyNames = new List<Address>();
        foreach (var address in pendingNames)
        {
            var currentName = Nexus.RootChain.GetNameFromAddress(Nexus.RootStorage, address, CurrentTime);
            if (currentName != ValidationUtils.ANONYMOUS_NAME)
            {
                readyNames.Add(address);
            }
        }
        foreach (var address in readyNames)
        {
            pendingNames.Remove(address);
        }

        blockOpen = true;

        step++;
        Log($"Begin block #{step}");
    }

    private void Log(string msg)
    {

    }

    public void CancelBlock()
    {
        if (!blockOpen)
        {
            throw new Exception("Simulator block not started");
        }

        blockOpen = false;
        Log($"Cancel block #{step}");
        step--;
    }

    public IEnumerable<Block> EndBlock()
    {
        if (!blockOpen)
        {
            throw new Exception("Simulator block not open");
        }

        usedAddresses.Clear();

        blockOpen = false;

        var blocks = new List<Block>();
        var protocol = (uint)Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);

        if (txChainMap.Count > 0)
        {
            var chains = txChainMap.Values.Distinct();

            foreach (var chain in chains)
            {
                var hashes = txChainMap.Where((p) => p.Value == chain).Select(x => x.Key);
                if (hashes.Any())
                {
                    var txs = new List<Transaction>();
                    foreach (var hash in hashes)
                    {
                        txs.Add(txHashMap[hash]);
                    }

                    var lastBlockHash = chain.GetLastBlockHash();
                    var lastBlock = chain.GetBlockByHash(lastBlockHash);
                    BigInteger nextHeight = lastBlock != null ? lastBlock.Height + 1 : Chain.InitialHeight;
                    var prevHash = lastBlock != null ? lastBlock.Hash : Hash.Null;

                    //var block = new Block(nextHeight, chain.Address, CurrentTime, prevHash, protocol, this.blockValidator.Address, System.Text.Encoding.UTF8.GetBytes("SIM"));
                    //block.AddAllTransactionHashes(hashes);

                    bool commited;

                    string reason = "unknown";

                    Block block = null;

                    try
                    {

                        var proposerAddress = _currentValidator.Address.TendermintAddress;

                        chain.ValidatorKeys = _currentValidator;

                        var pendingTxs = chain.BeginBlock(proposerAddress, chain.Height + 1, MinimumFee, this.CurrentTime, CurrentValidatorAddresses).ToList();
                        pendingTxs.AddRange(transactions);

                        foreach (var tx in pendingTxs)
                        {
                            var check = chain.CheckTx(tx, CurrentTime);

                            if (check.Item1 != CodeType.Ok)
                            {
                                FailedTxReason += "Transaction rejected: " + check.Item2 + "\n";
                                //throw new ChainException("Transaction rejected: "+ check.Item2);
                            }

                            var result = chain.DeliverTx(tx);

                            
                           /* try
                            {
                                var bytes = Serialization.Serialize(result.Result);
                                
                                var response = new ResponseDeliverTx()
                                {
                                    Code = result.Code,
                                    // Codespace cannot be null!
                                    Codespace = result.Codespace,
                                    Data = ByteString.CopyFrom(bytes),
                                };
                                
                                if (result.Events.Count() > 0)
                                {
                                    var newEvents = new List<Tendermint.Abci.Event>();
                                    foreach (var evt in result.Events)
                                    {
                                        var newEvent = new Tendermint.Abci.Event();
                                        var attributes = new EventAttribute[]
                                        {
                                            // Value cannot be null!
                                            new EventAttribute() { Key = "address", Value = evt.Address.ToString() },
                                            new EventAttribute() { Key = "contract", Value = evt.Contract },
                                            new EventAttribute() { Key = "data", Value = Base16.Encode(evt.Data) },
                                        };

                                        newEvent.Type = evt.Kind.ToString();
                                        newEvent.Attributes.AddRange(attributes);

                                        newEvents.Add(newEvent);
                                    }
                                    response.Events.AddRange(newEvents);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Entered here... with exception: " + e.Message);
                            }*/

                            if (result.State != ExecutionState.Halt)
                            {
                                // this is for debugging tests, feel free to put a breakpoint here or comment this line
                                Console.WriteLine("Transaction failed to execute properly: " + result.Codespace);
                                FailedTxReason += "Transaction failed to execute properly: " + result.Codespace + "\n";
                            }
                        }

                        var blockData = chain.EndBlock<Block>();

                        block = chain.CurrentBlock;

                        var commit = chain.Commit();

                        //block.Sign(this.blockValidator);

                        System.Diagnostics.Debug.WriteLine(block.Timestamp);

                        blocks.Add(block);

                        commited = true;
                    }
                    catch (Exception e)
                    {
                        FailedTx = true;
                        FailedTxReason += $"{e.Message}\n";
                        reason = e.Message;
                        commited = false;
                    }

                    if (commited)
                    {
                        int successCount = 0;
                        var blockHashes = block.TransactionHashes;
                        foreach (var hash in blockHashes)
                        {
                            var state = block.GetStateForTransaction(hash);
                            if (state == ExecutionState.Halt)
                            {
                                successCount++;
                            }
                        }

                        if (successCount == 0)
                        {
                            FailedTxReason += "Success Count = 0 \n";

                            //throw new ChainException("Transaction failed to execute properly: " + result.Codespace);
                        }

                        CurrentTime += blockTimeSkip;

                        Log($"End block #{step} @ {chain.Name} chain: {block.Hash}");
                    }
                    else
                    {
                        FailedTx = true;
                        FailedTxReason += $"add block @ {chain.Name} failed, reason: {reason} \n";

                        //throw new ChainException($"add block @ {chain.Name} failed, reason: {reason}");
                    }
                }
            }

            return blocks;
        }

        return Enumerable.Empty<Block>();
    }

    private Transaction MakeTransaction(IEnumerable<IKeyPair> signees, ProofOfWork pow, IChain chain, byte[] script)
    {
        if (!blockOpen)
        {
            throw new Exception("Call BeginBlock first");
        }
        
        Transaction tx = null;
        if (DomainSettings.LatestKnownProtocol <= 12)
        {
            tx = new Transaction(Nexus.Name, chain.Name, script, CurrentTime + TimeSpan.FromSeconds(40));
        }
        else
        {
            tx = new Transaction(Nexus.Name, chain.Name, script, CurrentTime + TimeSpan.FromSeconds(40));
        }

        Throw.If(!signees.Any(), "at least one signer required");
        
        var _user =signees.First();
        Signature[] existing = tx.Signatures;
        var msg = tx.ToByteArray(false);

        if (DomainSettings.LatestKnownProtocol <= 12)
        {
            tx = new Transaction(Nexus.Name, chain.Name, script, CurrentTime + TimeSpan.FromSeconds(40));
        }
        else
        {
            tx = new Transaction(Nexus.Name, chain.Name, script, CurrentTime + TimeSpan.FromSeconds(40), (_user as PhantasmaKeys).Address, Address.Null, MinimumFee, MinimumGasLimit);
        }

        tx.Mine((int)pow);

        foreach (var kp in signees)
        {
            tx.Sign(kp);
        }

        AddTransactionToPendingBlock(tx, chain);

        foreach (var signer in signees)
        {
            usedAddresses.Add(Address.FromKey(signer));
        }

        return tx;
    }

    private void AddTransactionToPendingBlock(Transaction tx, IChain chain)
    {
        txChainMap[tx.Hash] = chain as Chain;
        txHashMap[tx.Hash] = tx;
        transactions.Add(tx);
    }

    private Transaction MakeTransaction(IKeyPair source, ProofOfWork pow, IChain chain, byte[] script)
    {
        return MakeTransaction(new IKeyPair[] { source }, pow, chain, script);
    }

    public void SendRawTransaction(Transaction tx)
    {
        AddTransactionToPendingBlock(tx, Nexus.RootChain as Chain);
    }

    public Transaction GenerateCustomTransaction(IKeyPair owner, ProofOfWork pow, Func<byte[]> scriptGenerator)
    {
        return GenerateCustomTransaction(owner, pow, Nexus.RootChain as Chain, scriptGenerator);
    }

    public Transaction GenerateCustomTransaction(IKeyPair owner, ProofOfWork pow, Chain chain, Func<byte[]> scriptGenerator)
    {
        var script = scriptGenerator();

        var tx = MakeTransaction(owner, pow, chain, script);
        return tx;
    }

    public Transaction GenerateCustomTransaction(IEnumerable<PhantasmaKeys> owners, ProofOfWork pow, Func<byte[]> scriptGenerator)
    {
        return GenerateCustomTransaction(owners, pow, Nexus.RootChain as Chain, scriptGenerator);
    }

    public Transaction GenerateCustomTransaction(IEnumerable<PhantasmaKeys> owners, ProofOfWork pow, Chain chain, Func<byte[]> scriptGenerator)
    {
        var script = scriptGenerator();
        var tx = MakeTransaction(owners, pow, chain, script);
        return tx;
    }

    public Transaction GenerateToken(PhantasmaKeys owner, string symbol, string name, BigInteger totalSupply,
            int decimals, TokenFlags flags, byte[] tokenScript = null, Dictionary<string, int> labels = null, IEnumerable<ContractMethod> customMethods = null, uint seriesID = 0)
    {
        var version = Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);
        if (labels == null)
        {
            labels = new Dictionary<string, int>();
        }

        if (tokenScript == null)
        {
            // small script that restricts minting of tokens to transactions where the owner is a witness
            var addressStr = Base16.Encode(owner.Address.ToByteArray());
            string[] scriptString;

            if (version >= 4)
            {
                scriptString = new string[] {
                $"alias r3, $result",
                $"alias r4, $owner",
                $"@{AccountTrigger.OnMint}: nop",
                $"load $owner 0x{addressStr}",
                "push $owner",
                "extcall \"Address()\"",
                "extcall \"Runtime.IsWitness\"",
                "pop $result",
                $"jmpif $result, @end",
                $"load r0 \"invalid witness\"",
                $"throw r0",

                $"@getOwner: nop",
                $"load $owner 0x{addressStr}",
                "push $owner",
                $"jmp @end",

                $"@getSymbol: nop",
                $"load r0 \""+symbol+"\"",
                "push r0",
                $"jmp @end",

                $"@getName: nop",
                $"load r0 \""+name+"\"",
                "push r0",
                $"jmp @end",

                $"@getMaxSupply: nop",
                $"load r0 "+totalSupply+"",
                "push r0",
                $"jmp @end",

                $"@getDecimals: nop",
                $"load r0 "+decimals+"",
                "push r0",
                $"jmp @end",

                $"@getTokenFlags: nop",
                $"load r0 "+(int)flags+"",
                "push r0",
                $"jmp @end",

                $"@end: ret"
                };
            }
            else {
                scriptString = new string[] {
                $"alias r1, $triggerMint",
                $"alias r2, $currentTrigger",
                $"alias r3, $result",
                $"alias r4, $owner",

                $@"load $triggerMint, ""{AccountTrigger.OnMint}""",
                $"pop $currentTrigger",

                $"equal $triggerMint, $currentTrigger, $result",
                $"jmpif $result, @mintHandler",
                $"jmp @end",

                $"@mintHandler: nop",
                $"load $owner 0x{addressStr}",
                "push $owner",
                "extcall \"Address()\"",
                "extcall \"Runtime.IsWitness\"",
                "pop $result",
                $"jmpif $result, @end",
                $"load r0 \"invalid witness\"",
                $"throw r0",

                $"@end: ret"
                };
            }
            DebugInfo debugInfo;
            tokenScript = AssemblerUtils.BuildScript(scriptString, "GenerateToken",  out debugInfo, out labels);
        }

        var sb = ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, MinimumFee, DefaultGasLimit);

        if (version >= 4)
        {
            var triggerMap = new Dictionary<AccountTrigger, int>();

            var onMintLabel = AccountTrigger.OnMint.ToString();
            if (labels.ContainsKey(onMintLabel))
            {
                triggerMap[AccountTrigger.OnMint] = labels[onMintLabel];
            }

            var methods = AccountContract.GetTriggersForABI(triggerMap);

            if (version >= 6)
            {
                methods = methods.Concat(new ContractMethod[] {
                    new ContractMethod("getOwner", VMType.Object, labels, new ContractParameter[0]),
                    new ContractMethod("getSymbol", VMType.String, labels, new ContractParameter[0]),
                    new ContractMethod("getName", VMType.String, labels, new ContractParameter[0]),
                    new ContractMethod("getDecimals", VMType.Number, labels, new ContractParameter[0]),
                    new ContractMethod("getMaxSupply", VMType.Number, labels, new ContractParameter[0]),
                    new ContractMethod("getTokenFlags", VMType.Enum, labels, new ContractParameter[0]),
                }) ;
            }

            if (customMethods != null)
            {
                methods = methods.Concat(customMethods);
            }

            var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
            var abiBytes = abi.ToByteArray();

            object[] args;

            if (version >= 6)
            {
                args = new object[] { owner.Address, tokenScript, abiBytes };
            }
            else
            {
                args = new object[] { owner.Address, symbol, name, totalSupply, decimals, flags, tokenScript, abiBytes };
            }

            sb.CallInterop("Nexus.CreateToken", args);
        }
        else
        {
            sb.CallInterop("Nexus.CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags, tokenScript);
        }

        if (!flags.HasFlag(TokenFlags.Fungible))
        {
            ContractInterface nftABI;
            byte[] nftScript;
            TokenUtils.GenerateNFTDummyScript(symbol, name, name, "http://simulator/nft/*", "http://simulator/img/*", out nftScript, out nftABI);
            sb.CallInterop("Nexus.CreateTokenSeries", owner.Address, symbol, new BigInteger(seriesID), totalSupply, TokenSeriesMode.Unique, nftScript, nftABI.ToByteArray());
        }

        sb.SpendGas(owner.Address);
        
        var script = sb.EndScript();

        var tx = MakeTransaction(owner, ProofOfWork.Minimal, Nexus.RootChain as Chain, script);

        return tx;
    }

    public Transaction MintTokens(PhantasmaKeys owner, Address destination, string symbol, BigInteger amount)
    {
        var chain = Nexus.RootChain;

        var script = ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, MinimumFee, DefaultGasLimit).
            MintTokens(symbol, owner.Address, destination, amount).
            SpendGas(owner.Address).
            EndScript();

        var tx = MakeTransaction(owner, ProofOfWork.None, chain as Chain, script);

        return tx;
    }

    public Transaction GenerateSideChainSend(PhantasmaKeys source, string tokenSymbol, IChain sourceChain, Address targetAddress, IChain targetChain, BigInteger amount, BigInteger fee)
    {
        Throw.IfNull(source, nameof(source));
        Throw.If(!Nexus.TokenExists(Nexus.RootStorage, tokenSymbol), "Token does not exist: "+ tokenSymbol);
        Throw.IfNull(sourceChain, nameof(sourceChain));
        Throw.IfNull(targetChain, nameof(targetChain));
        Throw.If(amount <= 0, "positive amount required");

        if (source.Address == targetAddress && tokenSymbol == DomainSettings.FuelTokenSymbol)
        {
            Throw.If(fee != 0, "no fees for same address");
        }
        else
        {
            Throw.If(fee <= 0, "fee required when target is different address or token not native");
        }

        var sb = ScriptUtils.
            BeginScript().
            AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit);

        if (targetAddress != source.Address)
        {
            sb.CallInterop("Runtime.SwapTokens", targetChain.Name, source.Address, targetAddress, DomainSettings.FuelTokenSymbol, fee);
        }

        var script =
            sb.CallInterop("Runtime.SwapTokens", targetChain.Name, source.Address, targetAddress, tokenSymbol, amount).
            SpendGas(source.Address).
            EndScript();

        var tx = MakeTransaction(source, ProofOfWork.None, sourceChain, script);

        return tx;
    }

    public Transaction GenerateSideChainSettlement(PhantasmaKeys source, IChain sourceChain, IChain destChain, Transaction transaction)
    {
        var script = ScriptUtils.
            BeginScript().
            CallContract(NativeContractKind.Block, nameof(BlockContract.SettleTransaction), sourceChain.Address, transaction.Hash).
            AllowGas(source.Address, Address.Null, MinimumFee, 800).
            SpendGas(source.Address).
            EndScript();
        var tx = MakeTransaction(source, ProofOfWork.None, destChain, script);
        return tx;
    }

    public Transaction GenerateAccountRegistration(PhantasmaKeys source, string name)
    {
        var sourceChain = this.Nexus.RootChain;
        var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit).CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterName), source.Address, name).SpendGas(source.Address).EndScript();
        var tx = MakeTransaction(source, ProofOfWork.Minimal, sourceChain as Chain, script);

        pendingNames.Add(source.Address);
        return tx;
    }

    public Transaction GenerateChain(PhantasmaKeys source, string organization, string parentchain, string name)
    {
        Throw.IfNull(parentchain, nameof(parentchain));

        var script = ScriptUtils.BeginScript().
            AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit).
            CallInterop("Nexus.CreateChain", source.Address, organization, name, parentchain).
            SpendGas(source.Address).EndScript();

        var tx = MakeTransaction(source, ProofOfWork.Minimal, Nexus.RootChain as Chain , script);
        return tx;
    }

    public Transaction DeployContracts(PhantasmaKeys source, Chain chain, params string[] contracts)
    {

        var sb = ScriptUtils.BeginScript().
            AllowGas(source.Address, Address.Null, MinimumFee, 999);

        foreach (var contractName in contracts)
        {
            sb.CallInterop("Runtime.DeployContract", source.Address, contractName);
        }

        var script = sb.SpendGas(source.Address).
            EndScript();

        var tx = MakeTransaction(source, ProofOfWork.Minimal, chain, script);
        return tx;
    }

    public Transaction GenerateTransfer(PhantasmaKeys source, Address dest, IChain chain, string tokenSymbol, BigInteger amount, List<PhantasmaKeys> signees = null)
    {
        signees = signees ?? new List<PhantasmaKeys>();
        var found = false;
        foreach (var signer in signees)
        {
            if (signer.Address == source.Address)
            {
                found = true;
            }
        }

        if (!found)
        {
            signees.Add(source);
        }

        var script = ScriptUtils.BeginScript().
            AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit).
            TransferTokens(tokenSymbol, source.Address, dest, amount).
            SpendGas(source.Address).
            EndScript();

        var tx = MakeTransaction(signees, ProofOfWork.None, chain, script);
        return tx;
    }

    public Transaction GenerateSwapFee(PhantasmaKeys source, IChain chain, string fromSymbol, BigInteger amount)
    {
        var tx = GenerateCustomTransaction(source, ProofOfWork.None, () => 
            ScriptUtils.BeginScript()
            .CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), source.Address, fromSymbol, amount)
            .AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit)
            .SpendGas(source.Address)
            .EndScript());
        //var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
        return tx;
    }
    
    public Transaction GenerateSwap(PhantasmaKeys source, IChain chain, string fromSymbol, string toSymbol, BigInteger amount)
    {
        var script = ScriptUtils.BeginScript().
            CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapTokens), source.Address, fromSymbol, toSymbol, amount).
            AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit).
            SpendGas(source.Address).
            EndScript();
        var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
        return tx;
    }

    public Transaction GenerateNftTransfer(PhantasmaKeys source, Address dest, IChain chain, string tokenSymbol, BigInteger tokenId)
    {
        var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit).CallInterop("Runtime.TransferToken", source.Address, dest, tokenSymbol, tokenId).SpendGas(source.Address).EndScript();
        var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
        return tx;
    }

    public Transaction GenerateNftBurn(PhantasmaKeys source, IChain chain, string tokenSymbol, BigInteger tokenId)
    {
        var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit).CallInterop("Runtime.BurnToken", source.Address, tokenSymbol, tokenId).SpendGas(source.Address).EndScript();
        var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
        return tx;
    }

    public Transaction GenerateNftSale(PhantasmaKeys source, IChain chain, string tokenSymbol, BigInteger tokenId, BigInteger price)
    {
        Timestamp endDate = this.CurrentTime + TimeSpan.FromDays(5);
        var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit).CallContract(NativeContractKind.Market, nameof(MarketContract.SellToken), source.Address, tokenSymbol, DomainSettings.FuelTokenSymbol, tokenId, price, endDate).SpendGas(source.Address).EndScript();
        var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
        return tx;
    }

    public Transaction MintNonFungibleToken(PhantasmaKeys owner, Address destination, string tokenSymbol, byte[] rom, byte[] ram, BigInteger seriesID)
    {
        var chain = Nexus.RootChain;
        var script = ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, MinimumFee, DefaultGasLimit).
            CallInterop("Runtime.MintToken", owner.Address, destination, tokenSymbol, rom, ram, seriesID).  
            SpendGas(owner.Address).
            EndScript();

        var tx = MakeTransaction(owner, ProofOfWork.None, chain as Chain, script);
        return tx;
    }

    public Transaction InfuseNonFungibleToken(PhantasmaKeys owner, string tokenSymbol, BigInteger tokenID, string infuseSymbol, BigInteger value)
    {
        var chain = Nexus.RootChain;
        var script = ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, MinimumFee, DefaultGasLimit).
            CallInterop("Runtime.InfuseToken", owner.Address, tokenSymbol, tokenID, infuseSymbol, value).
            SpendGas(owner.Address).
            EndScript();

        var tx = MakeTransaction(owner, ProofOfWork.None, chain as Chain, script);
        return tx;
    }

    public Transaction GenerateSetTokenMetadata(PhantasmaKeys source, string tokenSymbol, string key, string value)
    {
        var chain = Nexus.RootChain;
        var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, DefaultGasLimit).CallInterop("Runtime.SetMetadata", source.Address, tokenSymbol, key, value).SpendGas(source.Address).EndScript();
        var tx = MakeTransaction(source, ProofOfWork.None, chain as Chain, script);

        return tx;
    }

    private int step;
    private HashSet<Address> usedAddresses = new HashSet<Address>();

    public void GenerateRandomBlock()
    {
        BeginBlock();

        int transferCount = 1 + _rnd.Next() % 10;
        int tries = 0;
        while (tries < 10000)
        {
            if (transactions.Count >= transferCount)
            {
                break;
            }

            tries++;
            var source = _keys[_rnd.Next() % _keys.Count];

            if (usedAddresses.Contains(source.Address))
            {
                continue;
            }

            var prevTxCount = transactions.Count;

            var sourceChain = Nexus.RootChain;
            var fee = DefaultGasLimit;

            string tokenSymbol;

            switch (_rnd.Next() % 4)
            {
                case 1: tokenSymbol = DomainSettings.FiatTokenSymbol; break;
                //case 2: token = Nexus.FuelTokenSymbol; break;
                default: tokenSymbol = DomainSettings.StakingTokenSymbol; break;
            }

            switch (_rnd.Next() % 7)
            {
                /*
                // side-chain send
                case 1:
                    {
                        var sourceChainList = Nexus.Chains.ToArray();
                        sourceChain = Nexus.GetChainByName( sourceChainList[_rnd.Next() % sourceChainList.Length]);

                        var targetChainList = Nexus.Chains.Select(x => Nexus.GetChainByName(x)).Where(x => Nexus.GetParentChainByName(x.Name) == sourceChain.Name || Nexus.GetParentChainByName(sourceChain.Name) == x.Name).ToArray();
                        var targetChain = targetChainList[_rnd.Next() % targetChainList.Length];

                        var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, DomainSettings.FuelTokenDecimals);

                        var tokenBalance = sourceChain.GetTokenBalance(sourceChain.Storage, tokenSymbol, source.Address);
                        var fuelBalance = sourceChain.GetTokenBalance(sourceChain.Storage, DomainSettings.FuelTokenSymbol, source.Address);

                        var expectedTotal = total;
                        if (tokenSymbol == DomainSettings.FuelTokenSymbol)
                        {
                            expectedTotal += fee;
                        }

                        var sideFee = 0;
                        if (tokenSymbol != DomainSettings.FuelTokenSymbol)
                        {
                            sideFee = fee;
                        }

                        if (tokenBalance > expectedTotal && fuelBalance > fee + sideFee)
                        {
                            Log($"Rnd.SideChainSend: {total} {tokenSymbol} from {source.Address}");
                            GenerateSideChainSend(source, tokenSymbol, sourceChain, source.Address, targetChain, total, sideFee);
                        }
                        break;
                    }

                // side-chain receive
                case 2:
                    {
                        if (_pendingBlocks.Any())
                        {
                            var pendingBlock = _pendingBlocks.First();

                            if (mempool == null || Nexus.GetConfirmationsOfHash(pendingBlock.hash) > 0)
                            {

                                var balance = pendingBlock.destChain.GetTokenBalance(pendingBlock.destChain.Storage, pendingBlock.tokenSymbol, source.Address);
                                if (balance > 0)
                                {
                                    Log($"...Settling {pendingBlock.sourceChain.Name}=>{pendingBlock.destChain.Name}: {pendingBlock.hash}");
                                    GenerateSideChainSettlement(source, pendingBlock.sourceChain, pendingBlock.destChain, pendingBlock.hash);
                                }
                            }
                        }

                        break;
                    }
                    */
                /*
            // stable claim
            case 3:
                {
                    sourceChain = bankChain;
                    tokenSymbol = Nexus.FuelTokenSymbol;

                    var balance = sourceChain.GetTokenBalance(tokenSymbol, source.Address);

                    var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, Nexus.FuelTokenDecimals - 1);

                    if (balance > total + fee)
                    {
                        Log($"Rnd.StableClaim: {total} {tokenSymbol} from {source.Address}");
                        GenerateStableClaim(source, sourceChain, total);
                    }

                    break;
                }

            // stable redeem
            case 4:
                {
                    sourceChain = bankChain;
                    tokenSymbol = Nexus.FiatTokenSymbol;

                    var tokenBalance = sourceChain.GetTokenBalance(tokenSymbol, source.Address);
                    var fuelBalance = sourceChain.GetTokenBalance(Nexus.FuelTokenSymbol, source.Address);

                    var rate = (BigInteger) bankChain.InvokeContract("bank", "GetRate", Nexus.FuelTokenSymbol);
                    var total = tokenBalance / 10;
                    if (total >= rate && fuelBalance > fee)
                    {
                        Log($"Rnd.StableRedeem: {total} {tokenSymbol} from {source.Address}");
                        GenerateStableRedeem(source, sourceChain, total);
                    }

                    break;
                }*/

                // name register
                case 5:
                    {
                        sourceChain = this.Nexus.RootChain;
                        tokenSymbol = DomainSettings.FuelTokenSymbol;

                        var token = Nexus.GetTokenInfo(Nexus.RootStorage, tokenSymbol);

                        var balance = sourceChain.GetTokenBalance(sourceChain.Storage, token, source.Address);
                        if (balance > fee + AccountContract.RegistrationCost && !pendingNames.Contains(source.Address))
                        {
                            var randomName = accountNames[_rnd.Next() % accountNames.Length];

                            switch (_rnd.Next() % 10)
                            {
                                case 1:
                                case 2:
                                    randomName += (_rnd.Next() % 10).ToString();
                                    break;

                                case 3:
                                case 4:
                                case 5:
                                    randomName += (10 + _rnd.Next() % 90).ToString();
                                    break;

                                case 6:
                                    randomName += (100 + _rnd.Next() % 900).ToString();
                                    break;
                            }

                            var currentName = Nexus.RootChain.GetNameFromAddress(Nexus.RootStorage, source.Address, CurrentTime);
                            if (currentName == ValidationUtils.ANONYMOUS_NAME)
                            {
                                var lookup = Nexus.LookUpName(Nexus.RootStorage, randomName, CurrentTime);
                                if (lookup.IsNull)
                                {
                                    Log($"Rnd.GenerateAccount: {source.Address} => {randomName}");
                                    GenerateAccountRegistration(source, randomName);
                                }
                            }
                        }

                        break;
                    }

                // normal transfer
                default:
                    {
                        var temp = _rnd.Next() % 5;
                        Address targetAddress;

                        if ((_keys.Count < 2 || temp == 0) && _keys.Count < 2000)
                        {
                            var key = PhantasmaKeys.Generate();
                            _keys.Add(key);
                            targetAddress = key.Address;
                        }
                        else
                        {
                            targetAddress = _keys[_rnd.Next() % _keys.Count].Address;
                        }

                        if (source.Address != targetAddress)
                        {
                            var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, DomainSettings.FuelTokenDecimals - 1);

                            var token = Nexus.GetTokenInfo(Nexus.RootStorage, tokenSymbol);
                            var tokenBalance = sourceChain.GetTokenBalance(sourceChain.Storage, token, source.Address);

                            var fuelToken = Nexus.GetTokenInfo(Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
                            var fuelBalance = sourceChain.GetTokenBalance(sourceChain.Storage, fuelToken, source.Address);

                            var expectedTotal = total;
                            if (tokenSymbol == DomainSettings.FuelTokenSymbol)
                            {
                                expectedTotal += fee;
                            }

                            if (tokenBalance > expectedTotal && fuelBalance > fee)
                            {
                                Log($"Rnd.Transfer: {total} {tokenSymbol} from {source.Address} to {targetAddress}");
                                GenerateTransfer(source, targetAddress, sourceChain as Chain, tokenSymbol, total);
                            }
                        }
                        break;
                    }
            }
        }

        if (transactions.Count > 0)
        {
            EndBlock();
        }
        else{
            CancelBlock();
        }
    }
    public Block TimeSkipMinutes(int minutes)
    {
        CurrentTime = CurrentTime.AddMinutes(minutes);
        DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);

        return ApplyTimeSkip();
    }
    public Block TimeSkipHours(int hours)
    {
        CurrentTime = CurrentTime.AddHours(hours);
        DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);

        return ApplyTimeSkip();
    }

    public Block TimeSkipYears(int years)
    {
        CurrentTime = CurrentTime.AddYears(years);
        DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);

        return ApplyTimeSkip();
    }

    public Block TimeSkipToDate(DateTime date)
    {
        CurrentTime = date;
        DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);

        return ApplyTimeSkip();
    }

    public Block TimeSkipDays(double days, bool roundUp = false)
    {
        CurrentTime = CurrentTime.AddDays(days);

        if (roundUp)
        {
            CurrentTime = CurrentTime.AddDays(1);
            CurrentTime = new DateTime(CurrentTime.Year, CurrentTime.Month, CurrentTime.Day, 0, 0, 0, DateTimeKind.Utc);

            var timestamp = (Timestamp)CurrentTime;
            var datetime = (DateTime)timestamp;
            if (datetime.Hour == 23)
                datetime = datetime.AddHours(2);

            CurrentTime = new DateTime(datetime.Year, datetime.Month, datetime.Day, datetime.Hour, 0, 0, DateTimeKind.Utc);   //to set the time of day component to 0
        }

        return ApplyTimeSkip();
    }

    private Block ApplyTimeSkip() 
    { 
        BeginBlock();
        var tx = GenerateCustomTransaction(_currentValidator, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(_currentValidator.Address, Address.Null, MinimumFee, DefaultGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.GetTimeBeforeUnstake), _currentValidator.Address)
                .SpendGas(_currentValidator.Address)
                .EndScript());
        
        var blocks = EndBlock();
        Assert.True(LastBlockWasSuccessful(), FailedTxReason);


        var txCost = Nexus.RootChain.GetTransactionFee(tx);

        return blocks.First();
    }
    public void WriteArchive(Hash hash, int blockIndex, byte[] bytes)
    {
        var archive = Nexus.GetArchive(Nexus.RootStorage, hash);
        if (archive == null)
        {
            throw new ChainException("archive not found");
        }

        if (blockIndex < 0 || blockIndex >= archive.BlockCount)
        {
            throw new ChainException("invalid block index");
        }

        Nexus.WriteArchiveBlock(archive, blockIndex, bytes);
    }

    public bool LastBlockWasSuccessful()
    {
        if ( FailedTx )
        {
            return false;
        }
        
        var chain = Nexus.RootChain;
        var block_hash = chain.GetBlockHashAtHeight(chain.Height);
        if (block_hash.IsNull)
        {
            return false;
        }

        var block = chain.GetBlockByHash(block_hash);
        if (block == null)
        {
            return false;
        }

        foreach (var tx_hash in block.TransactionHashes)
        {
            var state = block.GetStateForTransaction(tx_hash);
            if (state == ExecutionState.Fault)
            {
                return false;
            }
        }

        return true;
    }

    public VMObject InvokeContract(NativeContractKind nativeContract, string methodName, params object[] args)
    {
        return this.Nexus.RootChain.InvokeContractAtTimestamp(Nexus.RootStorage, CurrentTime, nativeContract, methodName, args);
    }

    public VMObject InvokeContract(string contractName, string methodName, params object[] args)
    {
        return this.Nexus.RootChain.InvokeContractAtTimestamp(Nexus.RootStorage, CurrentTime, contractName, methodName, args);
    }

    public VMObject InvokeScript(byte[] script)
    {
        return this.Nexus.RootChain.InvokeScript(Nexus.RootStorage, script, CurrentTime);
    }

    public void UpdateOraclePrice(string symbol, decimal price)
    {
        var oracle = this.Nexus.GetOracleReader() as OracleSimulator;
        oracle?.UpdatePrice(symbol, price); 
    }

}
