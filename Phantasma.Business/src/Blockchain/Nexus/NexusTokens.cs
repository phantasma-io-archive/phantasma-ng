using System;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Blockchain.Tokens.Structs;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Core;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Oracle.Structs;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.Triggers.Enums;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using DomainSettings = Phantasma.Core.Domain.DomainSettings;

//#define ALLOWANCE_OPERATIONS = true

namespace Phantasma.Business.Blockchain;

public partial class Nexus : INexus
{
    #region TOKENS

    public IToken CreateToken(StorageContext storage, string symbol, string name, Address owner, BigInteger maxSupply,
        int decimals, TokenFlags flags, byte[] script, ContractInterface abi = null)
    {
        Throw.IfNull(script, nameof(script));
        Throw.IfNull(abi, nameof(abi));

        var tokenInfo = new TokenInfo(symbol, name, owner, maxSupply, decimals, flags, script, abi);
        EditToken(storage, symbol, tokenInfo);

        // TODO_Migration, migrete TTRS with standard conform script!
        if (symbol == "TTRS") // support for 22series tokens with a dummy script that conforms to the standard
        {
            byte[] nftScript;
            ContractInterface nftABI;

            var url = "https://www.22series.com/part_info?id=*";
            Tokens.TokenUtils.GenerateNFTDummyScript(symbol, $"{symbol} #*", $"{symbol} #*", url, url, out nftScript,
                out nftABI);

            CreateSeries(storage, tokenInfo, 0, maxSupply, TokenSeriesMode.Unique, nftScript, nftABI);
        }
        else if (symbol == DomainSettings.RewardTokenSymbol)
        {
            byte[] nftScript;
            ContractInterface nftABI;

            var url = "https://phantasma.io/crown?id=*";
            Tokens.TokenUtils.GenerateNFTDummyScript(symbol, $"{symbol} #*", $"{symbol} #*", url, url, out nftScript,
                out nftABI);

            CreateSeries(storage, tokenInfo, 0, maxSupply, TokenSeriesMode.Unique, nftScript, nftABI);
        }

        // add to persistent list of tokens
        var tokenList = this.GetSystemList(TokenTag, storage);
        tokenList.Add(symbol);

        // we need to flush every chain ABI cache otherwise calls to the new token methods wont work
        var chainNames = GetChains(RootStorage);
        foreach (var chainName in chainNames)
        {
            var chain = GetChainByName(chainName) as Chain;
            chain.FlushExtCalls();
        }

        return tokenInfo;
    }

    private string GetTokenInfoKey(string symbol)
    {
        return ".token:" + symbol;
    }

    private void EditToken(StorageContext storage, string symbol, TokenInfo tokenInfo)
    {
        var key = GetTokenInfoKey(symbol);
        var bytes = Serialization.Serialize(tokenInfo);
        storage.Put(key, bytes);
    }

    public bool TokenExists(StorageContext storage, string symbol)
    {
        var key = GetTokenInfoKey(symbol);
        return storage.Has(key);
    }

    public bool IsSystemToken(string symbol)
    {
        if (DomainSettings.SystemTokens.Contains(symbol, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public IToken GetTokenInfo(StorageContext storage, string symbol)
    {
        var key = GetTokenInfoKey(symbol);
        if (storage.Has(key))
        {
            var bytes = storage.Get(key);
            var token = Serialization.Unserialize<TokenInfo>(bytes);

            TokenUtils.FetchProperty(storage, this.RootChain, "getOwner", token,
                (prop, value) => { token.Owner = value.AsAddress(); });

            return token;
        }

        throw new ChainException($"Token does not exist ({symbol})");
    }

    private static readonly string[] _dangerousSymbols = new[]
    {
        DomainSettings.StakingTokenSymbol,
        DomainSettings.FuelTokenSymbol,
        DomainSettings.FiatTokenSymbol,
        DomainSettings.RewardTokenSymbol,
        "ETH", "GAS", "NEO", "BNB", "USDT", "USDC", "DAI", "BTC"
    };

    public static bool IsDangerousSymbol(string symbol)
    {
        return _dangerousSymbols.Any(x => x.Equals(symbol, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsDangerousAddress(Address address, params Address[] ignoredAddresses)
    {
        foreach (var excludedAddress in ignoredAddresses)
        {
            if (excludedAddress == address)
            {
                return false;
            }
        }

        var nativeContract = NativeContract.GetNativeContractByAddress(address);
        if (nativeContract != null)
        {
            return true;
        }

        foreach (var symbol in _dangerousSymbols)
        {
            var tokenAddress = TokenUtils.GetContractAddress(symbol);
            if (tokenAddress == address)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// This method is used to mint tokens for the staking contract.
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="sourceChain"></param>
    /// <param name="amount"></param>
    private void MintStakingTokens(IRuntime Runtime, IToken token, Address source, Address destination,
        string sourceChain, BigInteger amount)
    {
        if (Runtime.ProtocolVersion <= 8)
        {
            Runtime.ExpectFiltered(Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName(),
                $"minting of {token.Symbol} can only happen via master claim", source);
        }
        else if (Runtime.ProtocolVersion <= 10)
        {
            var currentSupply = Runtime.GetTokenSupply(token.Symbol);
            var totalSupply = currentSupply + amount;
            var maxSupply = UnitConversion.ToBigInteger(
                decimal.Parse((100000000 * Math.Pow(1.03, ((DateTime)Runtime.Time).Year - 2018 - 1)).ToString()),
                DomainSettings.StakingTokenDecimals);
            if (Runtime.ProtocolVersion == 10)
            {
                // It should be 1784193 but we added more to the supply to avoid the need of a hardfork
                maxSupply += UnitConversion.ToBigInteger(5000000, DomainSettings.StakingTokenDecimals);
            }

            if (Runtime.CurrentContext.Name == "entry" && Runtime.IsPrimaryValidator(source) &&
                Runtime.IsPrimaryValidator(destination))
            {
                if (totalSupply <= maxSupply)
                {
                    Runtime.ExpectWarning(totalSupply <= maxSupply,
                        $"minting of {token.Symbol} can only happen if the amount is lower than 100M", source);
                    Runtime.ExpectWarning(Runtime.IsWitness(token.Owner),
                        $"minting of {token.Symbol} can only happen if the owner of the contract does it.",
                        source);
                    Runtime.ExpectWarning(Runtime.IsPrimaryValidator(source),
                        $"minting of {token.Symbol} can only happen if the owner of the contract does it.",
                        source);
                    Runtime.ExpectWarning(Runtime.IsPrimaryValidator(destination),
                        $"minting of {token.Symbol} can only happen if the destination is a validator.",
                        source);

                    var org = GetOrganizationByName(Runtime.RootStorage, DomainSettings.ValidatorsOrganizationName);
                    Runtime.ExpectWarning(org != null, "moving funds from null org currently not possible",
                        source);

                    var orgMembers = org.GetMembers();
                    // TODO: Check if it needs to be a DAO member
                    //Runtime.ExpectFiltered(orgMembers.Contains(destination), "destination must be a member of the org", destination);
                    Runtime.ExpectWarning(Runtime.Transaction.Signatures.Length == orgMembers.Length,
                        "must be signed by all org members", source);
                    var msg = Runtime.Transaction.ToByteArray(false);
                    foreach (var signature in Runtime.Transaction.Signatures)
                    {
                        Runtime.ExpectWarning(signature.Verify(msg, orgMembers), "invalid signature", source);
                    }
                }
            }
            else
            {
                bool isValidContext = Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName() ||
                                      Runtime.CurrentContext.Name == NativeContractKind.Gas.GetContractName();
                bool isValidOrigin = source == SmartContract.GetAddressForNative(NativeContractKind.Stake) ||
                                     source == SmartContract.GetAddressForNative(NativeContractKind.Gas);

                Runtime.ExpectWarning(isValidContext, $"minting of {token.Symbol} can only happen via master claim",
                    source);
                //Runtime.ExpectFiltered(source == destination, $"minting of {token.Symbol} can only happen if the owner of the contract.", source);
                Runtime.ExpectWarning(isValidOrigin,
                    $"minting of {token.Symbol} can only happen if it's the stake or gas address.", source);
            }
        }
        else
        {
            bool isValidEVM = source.IsEVMContext() && amount <= StakeContract.DefaultMasterThreshold;
            bool isValidContext = Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName() ||
                                  Runtime.CurrentContext.Name == NativeContractKind.Gas.GetContractName() || isValidEVM;
            bool isValidOrigin = source == SmartContract.GetAddressForNative(NativeContractKind.Stake) ||
                                 source == SmartContract.GetAddressForNative(NativeContractKind.Gas) || isValidEVM;

            Runtime.ExpectWarning(isValidContext, $"minting of {token.Symbol} can only happen via master claim",
                source);
            //Runtime.ExpectFiltered(source == destination, $"minting of {token.Symbol} can only happen if the owner of the contract.", source);
            Runtime.ExpectWarning(isValidOrigin,
                $"minting of {token.Symbol} can only happen if it's the stake or gas address.", source);
        }
    }


    /// <summary>
    /// Mint Fuel tokens.
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="sourceChain"></param>
    /// <param name="amount"></param>
    private void MintFuelTokens(IRuntime Runtime, IToken token, Address source, Address destination,
        string sourceChain, BigInteger amount)
    {
        if (Runtime.ProtocolVersion <= 8)
        {
            Runtime.ExpectFiltered(Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName(),
                $"minting of {token.Symbol} can only happen via claiming", source);
        }
        else if ( Runtime.ProtocolVersion <= 14)
        {
            Runtime.ExpectWarning(Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName(),
                $"minting of {token.Symbol} can only happen via claiming", source);
        }
        else if (Runtime.ProtocolVersion == 17)
        {
            // Mint tokens that were on other chains ETH/BSC/NEO and migrate them to Phantasma.
            var currentSupply = Runtime.GetTokenSupply(token.Symbol);
            var totalSupply = currentSupply + amount;
            var maxSupply = Runtime.GetTokenSupply(token.Symbol);
            
            if (Runtime.CurrentContext.Name == "entry" && Runtime.IsPrimaryValidator(source) &&
                Runtime.IsPrimaryValidator(destination))
            {
                Runtime.ExpectWarning(currentSupply <= UnitConversion.ToBigInteger(100000000m, DomainSettings.FuelTokenDecimals), $"minting of {token.Symbol} can only happen if the amount is lower than 100M", source);

                Runtime.ExpectWarning(Runtime.IsWitness(token.Owner),
                    $"minting of {token.Symbol} can only happen if the owner of the contract does it.",
                    source);
                Runtime.ExpectWarning(Runtime.IsPrimaryValidator(source),
                    $"minting of {token.Symbol} can only happen if the owner of the contract does it.",
                    source);
                Runtime.ExpectWarning(Runtime.IsPrimaryValidator(destination),
                    $"minting of {token.Symbol} can only happen if the destination is a validator.",
                    source);

                var org = GetOrganizationByName(Runtime.RootStorage, DomainSettings.ValidatorsOrganizationName);
                Runtime.ExpectWarning(org != null, "moving funds from null org currently not possible",
                    source);

                var orgMembers = org.GetMembers().ToList();
                var orgMembersCount = orgMembers.Count;
                
                Runtime.ExpectWarning(Runtime.Transaction.Signatures.Length == orgMembers.Count,
                    "Transaction must be signed by all Organization Members", source);
                var msg = Runtime.Transaction.ToByteArray(false);
                
                Runtime.ExpectWarning(org.IsWitness(Runtime.Transaction), "must be signed by the org", source);
                
                var witnessCount = 0;
                foreach (var signature in Runtime.Transaction.Signatures)
                {
                    foreach (var addr in orgMembers)
                    {
                        if (signature.Verify(msg, addr))
                        {
                            witnessCount++;
                            orgMembers.Remove(addr);
                            break;
                        }
                    }
                }
                
                Runtime.ExpectWarning(witnessCount == Runtime.Transaction.Signatures.Length, $"Transaction was not signed by every organization member, it's missing {orgMembersCount-witnessCount}", source);
            }
            else
            {
                Runtime.ExpectWarning(Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName(),
                    $"minting of {token.Symbol} can only happen via claiming", source);
            }
        }
        else
        {
            Runtime.ExpectWarning(Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName(),
                $"minting of {token.Symbol} can only happen via claiming", source);
        }
    }
    
    /// <summary>
    /// Mint tokens to an address
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="sourceChain"></param>
    /// <param name="amount"></param>
    public void MintTokens(IRuntime Runtime, IToken token, Address source, Address destination, string sourceChain,
        BigInteger amount)
    {
        Runtime.Expect(token.IsFungible(), "must be fungible");
        Runtime.Expect(amount > 0, "invalid amount");

        if (Runtime.HasGenesis)
        {
            if (token.Symbol == DomainSettings.StakingTokenSymbol)
            {
                MintStakingTokens(Runtime, token, source, destination, sourceChain, amount);
            }
            else if (token.Symbol == DomainSettings.FuelTokenSymbol)
            {
                MintFuelTokens(Runtime, token, source, destination, sourceChain, amount);
            }
            else
            {
                if (Runtime.ProtocolVersion <= 8)
                {
                    Runtime.ExpectFiltered(!IsDangerousSymbol(token.Symbol), $"minting of {token.Symbol} failed",
                        source);
                }
                else
                {
                    Runtime.ExpectWarning(!IsDangerousSymbol(token.Symbol), $"minting of {token.Symbol} failed",
                        source);
                }
            }
        }

        var isSettlement = sourceChain != Runtime.Chain.Name;

        var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);
        Runtime.Expect(supply.Mint(Runtime.Storage, amount, token.MaxSupply), "mint supply failed");

        var balances = new BalanceSheet(token);
        Runtime.Expect(balances.Add(Runtime.Storage, destination, amount), "balance add failed");

        if (!Runtime.IsSystemToken(token.Symbol))
        {
            // for non system tokens, the onMint trigger is mandatory
            var tokenTrigger = isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint;
            var tokenTriggerResult =
                Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, source, destination, token.Symbol, amount);
            Runtime.Expect(tokenTriggerResult == TriggerResult.Success,
                $"token trigger {tokenTrigger} failed or missing");
        }

        var accountTrigger = isSettlement ? ContractTrigger.OnReceive : ContractTrigger.OnMint;
        Runtime.Expect(
            Runtime.InvokeTriggerOnContract(true, destination, accountTrigger, source, destination, token.Symbol,
                amount) != TriggerResult.Failure, $"account trigger {accountTrigger} failed");

        if (isSettlement)
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, sourceChain));
            Runtime.Notify(EventKind.TokenClaim, destination,
                new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
        else
        {
            Runtime.Notify(EventKind.TokenMint, destination,
                new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
    }

    /// <summary>
    /// Mint Token - NFT version
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="sourceChain"></param>
    /// <param name="tokenID"></param>
    public void MintToken(IRuntime Runtime, IToken token, Address source, Address destination, string sourceChain,
        BigInteger tokenID)
    {
        Runtime.Expect(!token.IsFungible(), "cant be fungible");

        var isSettlement = sourceChain != Runtime.Chain.Name;

        var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);
        Runtime.Expect(supply.Mint(Runtime.Storage, 1, token.MaxSupply), "supply mint failed");

        var ownerships = new OwnershipSheet(token.Symbol);
        Runtime.Expect(ownerships.Add(Runtime.Storage, destination, tokenID), "ownership add failed");

        if (!Runtime.IsSystemToken(token.Symbol))
        {
            // for non system tokens, the onMint trigger is mandatory
            var tokenTrigger = isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint;
            var tokenTriggerResult =
                Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, source, destination, token.Symbol, tokenID);
            Runtime.Expect(tokenTriggerResult == TriggerResult.Success,
                $"token {tokenTrigger} trigger failed or missing");
        }

        var accountTrigger = isSettlement ? ContractTrigger.OnReceive : ContractTrigger.OnMint;
        Runtime.Expect(
            Runtime.InvokeTriggerOnContract(true, destination, accountTrigger, source, destination, token.Symbol,
                tokenID) != TriggerResult.Failure, $"account trigger {accountTrigger} failed");

        var nft = ReadNFT(Runtime, token.Symbol, tokenID);
        WriteNFT(Runtime, token.Symbol, tokenID, Runtime.Chain.Name, source, destination, nft.ROM, nft.RAM,
            nft.SeriesID, nft.Timestamp, nft.Infusion, !isSettlement);

        if (isSettlement)
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, sourceChain));
            Runtime.Notify(EventKind.TokenClaim, destination,
                new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
        else
        {
            Runtime.Notify(EventKind.TokenMint, destination,
                new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
    }

    private string GetBurnKey(string symbol)
    {
        return $".burned.{symbol}";
    }

    private void Internal_UpdateBurnedSupply(StorageContext storage, string burnKey, BigInteger burnAmount)
    {
        var burnedSupply = storage.Has(burnKey) ? storage.Get<BigInteger>(burnKey) : 0;
        burnedSupply += burnAmount;
        storage.Put<BigInteger>(burnKey, burnedSupply);
    }

    private void UpdateBurnedSupply(StorageContext storage, string symbol, BigInteger burnAmount)
    {
        var burnKey = GetBurnKey(symbol);
        Internal_UpdateBurnedSupply(storage, burnKey, burnAmount);
    }

    private void UpdateBurnedSupplyForSeries(StorageContext storage, string symbol, BigInteger burnAmount,
        BigInteger seriesID)
    {
        var burnKey = GetBurnKey($"{symbol}.{seriesID}");
        Internal_UpdateBurnedSupply(storage, burnKey, burnAmount);
    }

    public BigInteger GetBurnedTokenSupply(StorageContext storage, string symbol)
    {
        var burnKey = GetBurnKey(symbol);
        var burnedSupply = storage.Has(burnKey) ? storage.Get<BigInteger>(burnKey) : 0;
        return burnedSupply;
    }

    public BigInteger GetBurnedTokenSupplyForSeries(StorageContext storage, string symbol, BigInteger seriesID)
    {
        var burnKey = GetBurnKey($"{symbol}.{seriesID}");
        var burnedSupply = storage.Has(burnKey) ? storage.Get<BigInteger>(burnKey) : 0;
        return burnedSupply;
    }

    /// <summary>
    /// Burn Tokens
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="targetChain"></param>
    /// <param name="amount"></param>
    public void BurnTokens(IRuntime Runtime, IToken token, Address source, Address destination, string targetChain,
        BigInteger amount)
    {
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible");

        Runtime.Expect(amount > 0, "invalid amount");

        var allowed = Runtime.IsWitness(source);
        
        //Runtime.Expect(true, "TODO");

        Runtime.CheckFilterAmountThreshold(token, source, amount, "Burn Tokens");

#if ALLOWANCE_OPERATIONS
        if (!allowed)
        {
            allowed = Runtime.SubtractAllowance(source, token.Symbol, amount);
        }
#endif

        Runtime.Expect(allowed, "invalid witness or allowance");

        var isSettlement = targetChain != Runtime.Chain.Name;

        var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);

        Runtime.Expect(supply.Burn(Runtime.Storage, amount), $"{token.Symbol} burn failed");

        var balances = new BalanceSheet(token);
        Runtime.Expect(balances.Subtract(Runtime.Storage, source, amount),
            $"{token.Symbol} balance subtract failed from {source.Text}");

        // If trigger is missing the code will be executed
        Runtime.Expect(
            Runtime.InvokeTriggerOnToken(true, token, isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn, source,
                destination, token.Symbol, amount) != TriggerResult.Failure, "token trigger failed");

        // If trigger is missing the code will be executed
        Runtime.Expect(
            Runtime.InvokeTriggerOnContract(true, source,
                isSettlement ? ContractTrigger.OnSend : ContractTrigger.OnBurn, source, destination, token.Symbol,
                amount) != TriggerResult.Failure, "account trigger failed");

        if (isSettlement)
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            Runtime.Notify(EventKind.TokenStake, destination, new TokenEventData(token.Symbol, amount, targetChain));
        }
        else
        {
            UpdateBurnedSupply(Runtime.Storage, token.Symbol, amount);
            Runtime.Notify(EventKind.TokenBurn, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
    }

    /// <summary>
    /// Burn Token NFT Version
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="targetChain"></param>
    /// <param name="tokenID"></param>
    public void BurnToken(IRuntime Runtime, IToken token, Address source, Address destination, string targetChain,
        BigInteger tokenID)
    {
        Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), $"{token.Symbol} can't be fungible");

        var isSettlement = targetChain != Runtime.Chain.Name;

        var nft = Runtime.ReadToken(token.Symbol, tokenID);
        Runtime.Expect(nft.CurrentOwner != Address.Null, "nft already destroyed");
        Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "not on this chain");

        Runtime.Expect(nft.CurrentOwner == source, $"{source} is not the owner of {token.Symbol} #{tokenID}");

        Runtime.Expect(source != DomainSettings.InfusionAddress, $"{token.Symbol} #{tokenID} is currently infused");

        var chain = RootChain;
        var supply = new SupplySheet(token.Symbol, chain, this);

        Runtime.Expect(supply.Burn(Runtime.Storage, 1), "supply burning failed");

        if (Runtime.ProtocolVersion <= DomainSettings.Phantasma30Protocol)
        {
            DestroyNFTIfSettlement(Runtime, token, source, destination, tokenID, isSettlement);

            var ownerships = new OwnershipSheet(token.Symbol);
            Runtime.Expect(ownerships.Remove(Runtime.Storage, source, tokenID), "ownership removal failed");

            ValidateBurnTriggers(Runtime, token, source, destination, targetChain, tokenID, isSettlement);
        }
        else
        {
            ValidateBurnTriggers(Runtime, token, source, destination, targetChain, tokenID, isSettlement);

            DestroyNFTIfSettlement(Runtime, token, source, destination, tokenID, isSettlement);

            var ownerships = new OwnershipSheet(token.Symbol);
            Runtime.Expect(ownerships.Remove(Runtime.Storage, source, tokenID), "ownership removal failed");
        }

        if (isSettlement)
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            Runtime.Notify(EventKind.TokenStake, destination, new TokenEventData(token.Symbol, tokenID, targetChain));
            Runtime.Notify(EventKind.PackedNFT, destination, new PackedNFTData(token.Symbol, nft.ROM, nft.RAM));
        }
        else
        {
            UpdateBurnedSupply(Runtime.Storage, token.Symbol, 1);
            UpdateBurnedSupplyForSeries(Runtime.Storage, token.Symbol, 1, nft.SeriesID);
            Runtime.Notify(EventKind.TokenBurn, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
    }

    /// <summary>
    /// To validate the burn triggers to call the onBurn trigger on the token and on the account
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="targetChain"></param>
    /// <param name="tokenID"></param>
    /// <param name="isSettlement"></param>
    private void ValidateBurnTriggers(IRuntime Runtime, IToken token, Address source, Address destination,
        string targetChain, BigInteger tokenID, bool isSettlement)
    {
        // If trigger is missing the code will be executed
        var tokenTrigger = isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn;
        Runtime.Expect(
            Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, source, destination, token.Symbol, tokenID) !=
            TriggerResult.Failure, $"token {tokenTrigger} trigger failed: ");

        // If trigger is missing the code will be executed
        var accountTrigger = isSettlement ? ContractTrigger.OnSend : ContractTrigger.OnBurn;
        Runtime.Expect(
            Runtime.InvokeTriggerOnContract(true, source, accountTrigger, source, destination, token.Symbol, tokenID) !=
            TriggerResult.Failure, $"accont {accountTrigger} trigger failed: ");
    }

    private void DestroyNFTIfSettlement(IRuntime Runtime, IToken token, Address source, Address destination,
        BigInteger tokenID, bool isSettlement)
    {
        if (!isSettlement)
        {
            Runtime.Expect(source == destination, "source and destination must match when burning");
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
            DestroyNFT(Runtime, token.Symbol, tokenID, source);
        }
    }

    /// <summary>
    /// Infuse Token 
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="from"></param>
    /// <param name="tokenID"></param>
    /// <param name="infuseToken"></param>
    /// <param name="value"></param>
    public void InfuseToken(IRuntime Runtime, IToken token, Address from, BigInteger tokenID, IToken infuseToken,
        BigInteger value)
    {
        Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "can't be fungible");

        var nft = Runtime.ReadToken(token.Symbol, tokenID);
        Runtime.Expect(nft.CurrentOwner != Address.Null, "nft already destroyed");
        Runtime.Expect(nft.CurrentOwner == from, "nft does not belong to " + from);
        Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "not on this chain");

        if (token.Symbol == infuseToken.Symbol)
        {
            Runtime.Expect(value != tokenID, "cannot infuse token into itself");
        }

        var target = DomainSettings.InfusionAddress;

        // If trigger is missing the code will be executed
        var tokenTrigger = TokenTrigger.OnInfuse;
        Runtime.Expect(
            Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, from, target, infuseToken.Symbol, value) !=
            TriggerResult.Failure, $"token {tokenTrigger} trigger failed: ");

        if (infuseToken.IsFungible())
        {
            Runtime.CheckFilterAmountThreshold(infuseToken, from, value, "Infuse Tokens");
            this.TransferTokens(Runtime, infuseToken, from, target, value, true);
        }
        else
        {
            this.TransferToken(Runtime, infuseToken, from, target, value, true);
        }

        int index = -1;

        if (infuseToken.IsFungible())
        {
            for (int i = 0; i < nft.Infusion.Length; i++)
            {
                if (nft.Infusion[i].Symbol == infuseToken.Symbol)
                {
                    index = i;
                    break;
                }
            }
        }

        var infusion = nft.Infusion.ToList();

        if (index < 0)
        {
            infusion.Add(new TokenInfusion(infuseToken.Symbol, value));
        }
        else
        {
            var temp = nft.Infusion[index];
            infusion[index] = new TokenInfusion(infuseToken.Symbol, value + temp.Value);
        }

        WriteNFT(Runtime, token.Symbol, tokenID, nft.CurrentChain, nft.Creator, nft.CurrentOwner, nft.ROM, nft.RAM,
            nft.SeriesID, nft.Timestamp, infusion, true);

        Runtime.Notify(EventKind.Infusion, nft.CurrentOwner,
            new InfusionEventData(token.Symbol, tokenID, infuseToken.Symbol, value, nft.CurrentChain));
    }

    /// <summary>
    /// Transfer Tokens
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="amount"></param>
    /// <param name="isInfusion"></param>
    public void TransferTokens(IRuntime Runtime, IToken token, Address source, Address destination, BigInteger amount,
        bool isInfusion = false)
    {
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "Not transferable");
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible");

        Runtime.Expect(amount > 0, "invalid amount");
        Runtime.Expect(source != destination, "source and destination must be different");
        Runtime.Expect(!destination.IsNull, "invalid destination");
        Runtime.Expect(!source.IsNull, "invalid source");

        if (destination.IsSystem)
        {
            var destName = Runtime.Chain.GetNameFromAddress(Runtime.Storage, destination, Runtime.Time);
            Runtime.Expect(destName != ValidationUtils.ANONYMOUS_NAME, "anonymous system address as destination");
        }

        bool isOrganizationTransaction = false;
        this.ValidateTransferSystem(Runtime, source, destination, token.Symbol, amount, out isOrganizationTransaction,
            _infusionOperationAddress);

        this.ValidateTransferAmounts(Runtime, source, destination, token, amount, isOrganizationTransaction,
            _infusionOperationAddress);

        bool allowed = this.ValidateIsTransferAllow(Runtime, source, destination, token, amount,
            isOrganizationTransaction, _infusionOperationAddress);

        Runtime.Expect(allowed, "invalid witness or allowance");

        var balances = new BalanceSheet(token);
        Runtime.Expect(balances.Subtract(Runtime.Storage, source, amount),
            $"{token.Symbol} balance subtract failed from {source.Text}");
        Runtime.Expect(balances.Add(Runtime.Storage, destination, amount),
            $"{token.Symbol} balance add failed to {destination.Text}");

#if ALLOWANCE_OPERATIONS
        Runtime.AddAllowance(destination, token.Symbol, amount);
#endif

        Runtime.Expect(
            Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnSend, source, destination, token.Symbol, amount) !=
            TriggerResult.Failure, "token onSend trigger failed");
        Runtime.Expect(
            Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnReceive, source, destination, token.Symbol,
                amount) != TriggerResult.Failure, "token onReceive trigger failed");

        Runtime.Expect(
            Runtime.InvokeTriggerOnContract(true, source, ContractTrigger.OnSend, source, destination, token.Symbol,
                amount) != TriggerResult.Failure, "account onSend trigger failed");
        Runtime.Expect(
            Runtime.InvokeTriggerOnContract(true, destination, ContractTrigger.OnReceive, source, destination,
                token.Symbol, amount) != TriggerResult.Failure, "account onReceive trigger failed");

#if ALLOWANCE_OPERATIONS
        Runtime.RemoveAllowance(destination, token.Symbol);
#endif

        if (destination.IsSystem && (destination == Runtime.CurrentContext.Address || isInfusion))
        {
            Runtime.Notify(EventKind.TokenStake, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
        else if (source.IsSystem && (source == Runtime.CurrentContext.Address || isInfusion))
        {
            Runtime.Notify(EventKind.TokenClaim, destination,
                new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
        else
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            Runtime.Notify(EventKind.TokenReceive, destination,
                new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
    }

    /// <summary>
    /// Transfer Token NFT
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="tokenID"></param>
    /// <param name="isInfusion"></param>
    public void TransferToken(IRuntime Runtime, IToken token, Address source, Address destination, BigInteger tokenID,
        bool isInfusion = false)
    {
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "Not transferable");
        Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "Should be non-fungible");

        Runtime.Expect(tokenID > 0, "invalid nft id");

        Runtime.Expect(source != destination, "source and destination must be different");

        Runtime.Expect(!destination.IsNull, "destination cant be null");

        var nft = ReadNFT(Runtime, token.Symbol, tokenID);
        Runtime.Expect(nft.CurrentOwner != Address.Null, "nft already destroyed");

        var ownerships = new OwnershipSheet(token.Symbol);
        Runtime.Expect(ownerships.Remove(Runtime.Storage, source, tokenID), "ownership remove failed");

        Runtime.Expect(ownerships.Add(Runtime.Storage, destination, tokenID), "ownership add failed");

        Runtime.Expect(
            Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnSend, source, destination, token.Symbol,
                tokenID) != TriggerResult.Failure, "token send trigger failed");

        Runtime.Expect(
            Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnReceive, source, destination, token.Symbol,
                tokenID) != TriggerResult.Failure, "token receive trigger failed");

        Runtime.Expect(
            Runtime.InvokeTriggerOnContract(true, source, ContractTrigger.OnSend, source, destination, token.Symbol,
                tokenID) != TriggerResult.Failure, "account send trigger failed");

        Runtime.Expect(
            Runtime.InvokeTriggerOnContract(true, destination, ContractTrigger.OnReceive, source, destination,
                token.Symbol, tokenID) != TriggerResult.Failure, "account received trigger failed");

        WriteNFT(Runtime, token.Symbol, tokenID, Runtime.Chain.Name, nft.Creator, destination, nft.ROM, nft.RAM,
            nft.SeriesID, Runtime.Time, nft.Infusion, true);

        if (destination.IsSystem && (destination == Runtime.CurrentContext.Address || isInfusion))
        {
            Runtime.Notify(EventKind.TokenStake, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
        else if (source.IsSystem && (source == Runtime.CurrentContext.Address || isInfusion))
        {
            Runtime.Notify(EventKind.TokenClaim, destination,
                new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
        else
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            Runtime.Notify(EventKind.TokenReceive, destination,
                new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
    }

    public void MigrateTokenOwner(StorageContext storage, Address oldOwner, Address newOwner)
    {
        var symbols = GetAvailableTokenSymbols(storage);
        foreach (var symbol in symbols)
        {
            var token = (TokenInfo) GetTokenInfo(storage, symbol);
            if (token.Owner == oldOwner)
            {
                token.Owner = newOwner;
                EditToken(storage, symbol, token);
            }
        }
    }

    public IToken GetTokenInfo(StorageContext storage, Address contractAddress)
    {
        var symbols = GetAvailableTokenSymbols(storage);
        foreach (var symbol in symbols)
        {
            var tokenAddress = TokenUtils.GetContractAddress(symbol);

            if (tokenAddress == contractAddress)
            {
                var token = GetTokenInfo(storage, symbol);
                return token;
            }
        }

        return null;
    }
    
    public void UpgradeTokenContract(StorageContext storage, string symbol, byte[] script, ContractInterface abi)
    {
        var key = GetTokenInfoKey(symbol);
        if (!storage.Has(key))
        {
            throw new ChainException($"Cannot upgrade non-existing token contract: {symbol}");
        }

        if (IsDangerousSymbol(symbol))
        {
            throw new ChainException($"Forbidden to upgrade token contract: {symbol}");
        }

        var bytes = storage.Get(key);
        var info = Serialization.Unserialize<TokenInfo>(bytes);

        info = new TokenInfo(info.Symbol, info.Name, info.Owner, info.MaxSupply, info.Decimals, info.Flags, script, abi);
        bytes = Serialization.Serialize(info);
        storage.Put(key, bytes);
    }

    public SmartContract GetTokenContract(StorageContext storage, string symbol)
    {
        if (TokenExists(storage, symbol))
        {
            var token = GetTokenInfo(storage, symbol);

            return new CustomContract(symbol, token.Script, token.ABI);
        }

        return null;
    }

    public SmartContract GetTokenContract(StorageContext storage, Address contractAddress)
    {
        var token = GetTokenInfo(storage, contractAddress);
        if (token != null)
        {
            return new CustomContract(token.Symbol, token.Script, token.ABI);
        }

        return null;
    }
    #endregion
}
