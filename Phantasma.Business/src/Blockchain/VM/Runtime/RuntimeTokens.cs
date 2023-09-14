using System.Numerics;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.Triggers.Enums;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Numerics;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM : GasMachine, IRuntime
{
    /// <summary>
    /// Check if a token is a system token
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public bool IsSystemToken(string symbol)
    {
        return Nexus.IsSystemToken(symbol);
    }
    
    /// <summary>
    /// Create a new token
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="symbol"></param>
    /// <param name="name"></param>
    /// <param name="maxSupply"></param>
    /// <param name="decimals"></param>
    /// <param name="flags"></param>
    /// <param name="script"></param>
    /// <param name="abi"></param>
    public void CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, int decimals,
        TokenFlags flags, byte[] script, ContractInterface abi)
    {
        ExpectAddressSize(owner, nameof(owner));
        ExpectNameLength(symbol, nameof(symbol));
        ExpectNameLength(name, nameof(name));
        ExpectScriptLength(script, nameof(script));

        Expect(IsRootChain(), "must be root chain");

        Expect(owner.IsUser, "owner address must be user address");

        Expect(IsStakeMaster(owner), "needs to be master");
        Expect(IsWitness(owner), "invalid witness");

        var pow = Transaction.Hash.GetDifficulty();
        Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

        Expect(!string.IsNullOrEmpty(symbol), "token symbol required");
        Expect(!string.IsNullOrEmpty(name), "token name required");

        Expect(ValidationUtils.IsValidTicker(symbol), "invalid symbol");
        Expect(!TokenExists(symbol), "token already exists");

        Expect(maxSupply >= 0, "token supply cant be negative");
        Expect(decimals >= 0, "token decimals cant be negative");
        Expect(decimals <= DomainSettings.MAX_TOKEN_DECIMALS,
            $"token decimals cant exceed {DomainSettings.MAX_TOKEN_DECIMALS}");

        if (symbol == DomainSettings.FuelTokenSymbol)
        {
            Expect(flags.HasFlag(TokenFlags.Fuel), "token should be native");
        }
        else
        {
            Expect(!flags.HasFlag(TokenFlags.Fuel), "token can't be native");
        }

        if (symbol == DomainSettings.StakingTokenSymbol)
        {
            Expect(flags.HasFlag(TokenFlags.Stakable), "token should be stakable");
        }

        if (symbol == DomainSettings.FiatTokenSymbol)
        {
            Expect(flags.HasFlag(TokenFlags.Fiat), "token should be fiat");
        }

        if (!flags.HasFlag(TokenFlags.Fungible))
        {
            Expect(!flags.HasFlag(TokenFlags.Divisible), "non-fungible token must be indivisible");
        }

        if (flags.HasFlag(TokenFlags.Divisible))
        {
            Expect(decimals > 0, "divisible token must have decimals");
        }
        else
        {
            Expect(decimals == 0, "indivisible token can't have decimals");
        }

        var token = Nexus.CreateToken(RootStorage, symbol, name, owner, maxSupply, decimals, flags, script, abi);

        var constructor = abi.FindMethod(SmartContract.ConstructorName);

        if (constructor != null)
        {
            this.CallContext(symbol, constructor, owner);
        }

        var rootChain = (Chain)GetRootChain();
        var currentOwner = owner;
        TokenUtils.FetchProperty(RootStorage, rootChain, "getOwner", script, abi,
            (prop, value) => { currentOwner = value.AsAddress(); });

        Expect(!currentOwner.IsNull, "missing or invalid token owner");
        Expect(currentOwner == owner, "token owner constructor failure");

        var fuelCost = GetGovernanceValue(DomainSettings.FuelPerTokenDeployTag);
        // governance value is in usd fiat, here convert from fiat to fuel amount
        fuelCost = this.GetTokenQuote(DomainSettings.FiatTokenSymbol, DomainSettings.FuelTokenSymbol, fuelCost);

        var fuelBalance = GetBalance(DomainSettings.FuelTokenSymbol, owner);
        Expect(fuelBalance >= fuelCost,
            $"{UnitConversion.ToDecimal(fuelCost, DomainSettings.FuelTokenDecimals)} {DomainSettings.FuelTokenSymbol} required to create a token but {owner} has only {UnitConversion.ToDecimal(fuelBalance, DomainSettings.FuelTokenDecimals)} {DomainSettings.FuelTokenSymbol}");

        // burn the "cost" tokens
        BurnTokens(DomainSettings.FuelTokenSymbol, owner, fuelCost);

        this.Notify(EventKind.TokenCreate, owner, symbol);
    }

    /// <summary>
    /// Get Balance of a specific token for a specific address
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="address"></param>
    /// <returns></returns>
    public BigInteger GetBalance(string symbol, Address address)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(address, nameof(address));
        ExpectTokenExists(symbol);
        var token = GetToken(symbol);
        return Chain.GetTokenBalance(Storage, token, address);
    }

    /// <summary>
    /// Get NFT ID for a specific address and token symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="address"></param>
    /// <returns></returns>
    public BigInteger[] GetOwnerships(string symbol, Address address)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(address, nameof(address));
        ExpectTokenExists(symbol);
        return Chain.GetOwnedTokens(Storage, symbol, address);
    }

    /// <summary>
    /// Get Token Supply for a specific token symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public BigInteger GetTokenSupply(string symbol)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectTokenExists(symbol);
        return Chain.GetTokenSupply(Storage, symbol);
    }

    /// <summary>
    /// Check if a specific token exists
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public bool TokenExists(string symbol)
    {
        ExpectNameLength(symbol, nameof(symbol));
        return Nexus.TokenExists(RootStorage, symbol);
    }

    /// <summary>
    /// Check if a specific NFT exists
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="tokenID"></param>
    /// <returns></returns>
    public bool NFTExists(string symbol, BigInteger tokenID)
    {
        ExpectNameLength(symbol, nameof(symbol));

        return Nexus.HasNFT(RootStorage, symbol, tokenID);
    }

    /// <summary>
    /// Check if a specific Token exists on a specific platform
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="platform"></param>
    /// <returns></returns>
    public bool TokenExists(string symbol, string platform)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectNameLength(platform, nameof(platform));

        return Nexus.TokenExistsOnPlatform(symbol, platform, RootStorage);
    }

    /// <summary>
    /// Method is used to Mint Tokens
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="from"></param>
    /// <param name="target"></param>
    /// <param name="amount"></param>
    public void MintTokens(string symbol, Address from, Address target, BigInteger amount)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(from, nameof(from));
        ExpectAddressSize(target, nameof(target));

        if (HasGenesis)
        {
            if (IsSystemToken(symbol))
            {
                var ctxName = CurrentContext.Name;
                Expect(
                    ctxName == StakeContextName ||
                    ctxName == GasContextName ||
                    ctxName == ExchangeContextName ||
                    ctxName == EntryContextName,
                    $"Minting system tokens only allowed in a specific context, current {ctxName}");
            }
        }

        Expect(IsWitness(from), "must be from a valid witness");

        Expect(amount > 0, "amount must be positive and greater than zero");

        Expect(TokenExists(symbol), "invalid token");
        IToken token;
        token = GetToken(symbol);
        Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
        Expect(!token.Flags.HasFlag(TokenFlags.Fiat), "token can't be fiat");

        Nexus.MintTokens(this, token, from, target, Chain.Name, amount);
    }

    /// <summary>
    /// Method is used to Mint a NFT with a specific SeriesID
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="from"></param>
    /// <param name="target"></param>
    /// <param name="rom"></param>
    /// <param name="ram"></param>
    /// <param name="seriesID"></param>
    /// <returns></returns>
    public BigInteger MintToken(string symbol, Address from, Address target, byte[] rom, byte[] ram,
        BigInteger seriesID)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(from, nameof(from));
        ExpectAddressSize(target, nameof(target));

        if (IsSystemToken(symbol))
        {
            var ctxName = CurrentContext.Name;
            Expect(ctxName == "gas" || ctxName == "stake" || ctxName == "exchange",
                "Minting system tokens only allowed in a specific context");
        }

        Expect(TokenExists(symbol), "invalid token");
        IToken token;
        token = GetToken(symbol);
        Expect(!token.IsFungible(), "token must be non-fungible");

        // TODO should not be necessary, verified by trigger
        //Expect(IsWitness(target), "invalid witness");

        Expect(IsWitness(from), "must be from a valid witness");
        Expect(IsRootChain(), "can only mint nft in root chain");

        Expect(rom.Length <= TokenContent.MaxROMSize,
            "ROM size exceeds maximum allowed, received: " + rom.Length + ", maximum: " + TokenContent.MaxROMSize);
        Expect(ram.Length <= TokenContent.MaxRAMSize,
            "RAM size exceeds maximum allowed, received: " + ram.Length + ", maximum: " + TokenContent.MaxRAMSize);

        Address creator = from;

        BigInteger tokenID;
        tokenID = Nexus.GenerateNFT(this, symbol, Chain.Name, creator, rom, ram, seriesID);
        Expect(tokenID > 0, "invalid tokenID");

        Nexus.MintToken(this, token, from, target, Chain.Name, tokenID);

        return tokenID;
    }

    /// <summary>
    /// Method is used to Burn Tokens
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="target"></param>
    /// <param name="amount"></param>
    public void BurnTokens(string symbol, Address target, BigInteger amount)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(target, nameof(target));

        Expect(amount > 0, "amount must be positive and greater than zero");
        Expect(TokenExists(symbol), "invalid token");

        var token = GetToken(symbol);
        Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
        Expect(token.IsBurnable(), "token must be burnable");
        Expect(!token.Flags.HasFlag(TokenFlags.Fiat), "token can't be fiat");

        Nexus.BurnTokens(this, token, target, target, Chain.Name, amount);
    }

    /// <summary>
    /// Method is used to Burn a NFT
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="target"></param>
    /// <param name="tokenID"></param>
    public void BurnToken(string symbol, Address target, BigInteger tokenID)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(target, nameof(target));

        Expect(IsWitness(target), "invalid witness");
        Expect(IsRootChain(), "must be root chain");
        Expect(TokenExists(symbol), "invalid token");

        var token = GetToken(symbol);
        Expect(!token.IsFungible(), "token must be non-fungible");
        Expect(token.IsBurnable(), "token must be burnable");

        Nexus.BurnToken(this, token, target, target, Chain.Name, tokenID);
    }

    /// <summary>
    /// Method is used to Infuse a token with another token
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="from"></param>
    /// <param name="tokenID"></param>
    /// <param name="infuseSymbol"></param>
    /// <param name="value"></param>
    public void InfuseToken(string symbol, Address from, BigInteger tokenID, string infuseSymbol, BigInteger value)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(from, nameof(from));
        ExpectNameLength(infuseSymbol, nameof(infuseSymbol));

        Expect(IsWitness(from), "invalid witness");
        Expect(IsRootChain(), "must be root chain");
        Expect(TokenExists(symbol), "invalid token");

        var token = GetToken(symbol);
        Expect(!token.IsFungible(), "token must be non-fungible");
        Expect(token.IsBurnable(), "token must be burnable");

        var infuseToken = GetToken(infuseSymbol);

        Nexus.InfuseToken(this, token, from, tokenID, infuseToken, value);
    }

    /// <summary>
    /// Method is used to get a Token Series.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="seriesID"></param>
    /// <returns></returns>
    public ITokenSeries GetTokenSeries(string symbol, BigInteger seriesID)
    {
        ExpectNameLength(symbol, nameof(symbol));
        return Nexus.GetTokenSeries(RootStorage, symbol, seriesID);
    }

    /// <summary>
    /// Method is used to create a new token series.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="from"></param>
    /// <param name="seriesID"></param>
    /// <param name="maxSupply"></param>
    /// <param name="mode"></param>
    /// <param name="script"></param>
    /// <param name="abi"></param>
    /// <returns></returns>
    public ITokenSeries CreateTokenSeries(string symbol, Address from, BigInteger seriesID, BigInteger maxSupply,
        TokenSeriesMode mode, byte[] script, ContractInterface abi)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(from, nameof(from));
        ExpectEnumIsDefined(mode, nameof(mode));
        ExpectScriptLength(script, nameof(script));
        ExpectValidContractInterface(abi);

        Expect(seriesID >= 0, "invalid series ID");
        Expect(IsRootChain(), "must be root chain");
        Expect(TokenExists(symbol), "invalid token");

        var token = GetToken(symbol);
        Expect(!token.IsFungible(), "token must be non-fungible");

        Expect(IsWitness(from), "invalid witness");
        Expect(InvokeTriggerOnToken(false, token, TokenTrigger.OnSeries, from) != TriggerResult.Failure,
            $"trigger {TokenTrigger.OnSeries} on token {symbol} failed for {from}");

        return Nexus.CreateSeries(RootStorage, token, seriesID, maxSupply, mode, script, abi);
    }

    /// <summary>
    /// Transfer tokens from one address to another.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="amount"></param>
    public void TransferTokens(string symbol, Address source, Address destination, BigInteger amount)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(source, nameof(source));
        ExpectAddressSize(destination, nameof(destination));

        Expect(!source.IsNull, "invalid source");

        if (source == destination || amount == 0)
        {
            return;
        }

        Expect(TokenExists(symbol), "invalid token");
        var token = GetToken(symbol);

        Expect(amount > 0, "amount must be greater than zero");

        if (destination.IsInterop)
        {
            Expect(Chain.IsRoot, "interop transfers only allowed in main chain");
            this.CallNativeContext(NativeContractKind.Interop, nameof(InteropContract.WithdrawTokens), source,
                destination, symbol, amount);
            return;
        }

        Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
        Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

        Nexus.TransferTokens(this, token, source, destination, amount);
    }

    /// <summary>
    /// Method Used to Transfer NFT from one address to another
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="tokenID"></param>
    public void TransferToken(string symbol, Address source, Address destination, BigInteger tokenID)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectAddressSize(source, nameof(source));
        ExpectAddressSize(destination, nameof(destination));

        Expect(IsWitness(source), "invalid witness");
        Expect(source != destination, "source and destination must be different");
        Expect(TokenExists(symbol), "invalid token");

        var token = GetToken(symbol);
        Expect(!token.IsFungible(), "token must be non-fungible");

        Nexus.TransferToken(this, token, source, destination, tokenID);
    }

    /// <summary>
    /// Swap Token, this method is used to Cross Chain Swap Tokens
    /// </summary>
    /// <param name="sourceChain"></param>
    /// <param name="from"></param>
    /// <param name="targetChain"></param>
    /// <param name="to"></param>
    /// <param name="symbol"></param>
    /// <param name="value"></param>
    /// <exception cref="ChainException"></exception>
    public void SwapTokens(string sourceChain, Address from, string targetChain, Address to, string symbol,
        BigInteger value)
    {
        Expect(ProtocolVersion <= 15, "this method is obsolete");
        Expect(false, "this method is obsolete");

        ExpectNameLength(sourceChain, nameof(sourceChain));
        ExpectAddressSize(from, nameof(from));
        ExpectNameLength(targetChain, nameof(targetChain));
        ExpectAddressSize(to, nameof(to));
        ExpectNameLength(symbol, nameof(symbol));

        Expect(sourceChain != targetChain, "source chain and target chain must be different");
        Expect(TokenExists(symbol), "invalid token");

        var token = GetToken(symbol);
        Expect(token.Flags.HasFlag(TokenFlags.Transferable), "must be transferable token");

        if (PlatformExists(sourceChain))
        {
            Expect(sourceChain != DomainSettings.PlatformName, "invalid platform as source chain");

            /*if (token.IsFungible())
            {
                Nexus.MintTokens(this, token, from, to, sourceChain, value);
            }
            else
            {
                Nexus.MintToken(this, token, from, to, sourceChain, value);
            }*/
        }
        else if (PlatformExists(targetChain))
        {
            Expect(targetChain != DomainSettings.PlatformName, "invalid platform as target chain");
            //Nexus.BurnTokens(this, token, from, to, targetChain, value);

            var swap = new ChainSwap(DomainSettings.PlatformName, sourceChain, Transaction.Hash, targetChain,
                targetChain, Hash.Null);
            Chain.RegisterSwap(Storage, to, swap);
        }
        else if (sourceChain == Chain.Name)
        {
            Expect(IsNameOfParentChain(targetChain) || IsNameOfChildChain(targetChain),
                "target must be parent or child chain");
            Expect(to.IsUser, "destination must be user address");
            Expect(IsWitness(from), "invalid witness");

            /*if (tokenInfo.IsCapped())
            {
                var sourceSupplies = new SupplySheet(symbol, this.Chain, Nexus);
                var targetSupplies = new SupplySheet(symbol, targetChain, Nexus);

                if (IsAddressOfParentChain(targetChainAddress))
                {
                    Expect(sourceSupplies.MoveToParent(this.Storage, amount), "source supply check failed");
                }
                else // child chain
                {
                    Expect(sourceSupplies.MoveToChild(this.Storage, targetChain.Name, amount), "source supply check failed");
                }
            }*/

            /*if (token.IsFungible())
            {
                Nexus.BurnTokens(this, token, from, to, targetChain, value);
            }
            else
            {
                Nexus.BurnToken(this, token, from, to, targetChain, value);
            }*/

            var swap = new ChainSwap(DomainSettings.PlatformName, sourceChain, Transaction.Hash,
                DomainSettings.PlatformName, targetChain, Hash.Null);
            Chain.RegisterSwap(Storage, to, swap);
        }
        else if (targetChain == Chain.Name)
        {
            Expect(IsNameOfParentChain(sourceChain) || IsNameOfChildChain(sourceChain),
                "source must be parent or child chain");
            Expect(!to.IsInterop, "destination cannot be interop address");
            Expect(IsWitness(to), "invalid witness");

            if (token.IsFungible())
            {
                //Nexus.MintTokens(this, token, from, to, sourceChain, value);
            }
            else
            {
                //Nexus.MintToken(this, token, from, to, sourceChain, value);
            }
        }
        else
        {
            throw new ChainException("invalid swap chain source and destinations");
        }
    }

    /// <summary>
    /// Write Token, this method is used to write data to a NFT
    /// </summary>
    /// <param name="from"></param>
    /// <param name="tokenSymbol"></param>
    /// <param name="tokenID"></param>
    /// <param name="ram"></param>
    public void WriteToken(Address from, string tokenSymbol, BigInteger tokenID, byte[] ram)
    {
        ExpectAddressSize(from, nameof(from));
        ExpectNameLength(tokenSymbol, nameof(tokenSymbol));
        ExpectRamLength(ram, nameof(ram));

        var nft = ReadToken(tokenSymbol, tokenID);
        var token = GetToken(tokenSymbol);

        // If trigger is missing the code will be executed
        Expect(InvokeTriggerOnToken(true, token, TokenTrigger.OnWrite, from, ram, tokenID) != TriggerResult.Failure,
            "token write trigger failed");

        Nexus.WriteNFT(this, tokenSymbol, tokenID, nft.CurrentChain, nft.Creator, nft.CurrentOwner, nft.ROM, ram,
            nft.SeriesID, nft.Timestamp, nft.Infusion, true);
    }

    /// <summary>
    /// Read Token, this method is used to read data from a NFT
    /// </summary>
    /// <param name="tokenSymbol"></param>
    /// <param name="tokenID"></param>
    /// <returns></returns>
    public TokenContent ReadToken(string tokenSymbol, BigInteger tokenID)
    {
        ExpectNameLength(tokenSymbol, nameof(tokenSymbol));
        return Nexus.ReadNFT(this, tokenSymbol, tokenID);
    }

    /// <summary>
    /// Return all the Tokens available on Phantasma
    /// </summary>
    /// <returns></returns>
    public string[] GetTokens()
    {
        return Nexus.GetAvailableTokenSymbols(RootStorage);
    }

    /// <summary>
    /// Get Token Platform Hash, this method is used to get the Hash of a Token on a specific platform
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="platform"></param>
    /// <returns></returns>
    public Hash GetTokenPlatformHash(string symbol, IPlatform platform)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectValidPlatform(platform);

        if (platform == null)
        {
            return Hash.Null;
        }

        return Nexus.GetTokenPlatformHash(symbol, platform.Name, RootStorage);
    }

    /// <summary>
    /// Get Token
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public IToken GetToken(string symbol)
    {
        ExpectNameLength(symbol, nameof(symbol));
        return Nexus.GetTokenInfo(RootStorage, symbol);
    }
}
