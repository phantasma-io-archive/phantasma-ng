using System.Numerics;
using System.Collections.Generic;

using Phantasma.Core.Types;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract.Interop;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types.Structs;
using Phantasma.Infrastructure.Pay.Chains;

namespace Phantasma.Business.Tests.Simulator;

public struct SimulatorChainSwap
{
    public Hash hash;
    public string platformName;
    public Address sourceAddress;
    public Address interopAddress;
    public string Symbol;
    public decimal Amount;
}

public class OracleSimulator : OracleReader
{
    private static List<SimulatorChainSwap> _swaps = new List<SimulatorChainSwap>();

    private Dictionary<string, decimal> _tokenPrices = new Dictionary<string, decimal>();

    public OracleSimulator(Nexus nexus) : base(nexus)
    {

    }

    public static void CleanUp()
    {
        _swaps.Clear();
    }

    public override string GetCurrentHeight(string platformName, string chainName)
    {
        return "";
    }

    public override void SetCurrentHeight(string platformName, string chainName, string height)
    {

    }

    public override List<InteropBlock> ReadAllBlocks(string platformName, string chainName)
    {
        return new List<InteropBlock>();
    }

    public static Hash SimulateExternalTransaction(string platformName, byte platformID, byte[] publicKey, string publicAddress, string symbol, decimal amount)
    {
        var interopAddress = Address.FromInterop(platformID, publicKey);

        var bytes = interopAddress.ToByteArray();
        bytes[0] = (byte)AddressKind.User;
        interopAddress = Address.FromBytes(bytes);

        Address sourceAddress;

        switch (platformID)
        {
            case NeoWallet.NeoID:
                sourceAddress = NeoWallet.EncodeAddress(publicAddress);
                break;

            default:
                throw new System.NotImplementedException();
        }

        var swap = new SimulatorChainSwap()
        {
            hash = Hash.FromString(platformName + sourceAddress.Text + symbol + amount),
            Symbol = symbol,
            platformName = platformName,
            sourceAddress = sourceAddress,
            interopAddress = interopAddress,
            Amount = amount
        };

        _swaps.Add(swap);
        return swap.hash;
    }

    protected override T PullData<T>(Timestamp time, string url)
    {
        throw new OracleException("invalid oracle url: " + url);
    }

    protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash, BigInteger height)
    {
        InteropBlock interopBlock = null;
        switch (platformName)
        {
            case "neo":
                {
                    switch (chainName)
                    {
                        // we abuse chainName here to simulate different results
                        case "neoEmpty":
                            interopBlock = new InteropBlock(platformName, chainName, Hash.Null, new Hash[0]);
                            break;
                        case "neo":
                            interopBlock = new InteropBlock(platformName, chainName, Hash.FromString("neohash"), new Hash[0]);
                            break;
                    }
                }
                break;
            case "ethereum":
                {
                    switch (chainName)
                    {
                        // we abuse chainName here to simulate different results
                        case "ethereumEmpty":
                            interopBlock = new InteropBlock(platformName, chainName, Hash.Null, new Hash[0]);
                            break;
                        case "ethereum":
                            interopBlock = new InteropBlock(platformName, chainName, Hash.FromString("neohash"), new Hash[0]);
                            break;
                    }
                }
                break;
        }

        return interopBlock;

    }

    protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
    {
        foreach (var swap in _swaps)
        if (swap.platformName == platformName && chainName == platformName && swap.hash == hash)
        {
            var info = Nexus.GetPlatformInfo(Nexus.RootStorage, platformName);
            var platformAddress = info.InteropAddresses[0];

            var token = Nexus.GetTokenInfo(Nexus.RootStorage, swap.Symbol);
            var amount = UnitConversion.ToBigInteger(swap.Amount, token.Decimals);

                return new InteropTransaction(hash, new InteropTransfer[]
                {
                    new InteropTransfer(platformName, swap.sourceAddress, platformName, platformAddress.LocalAddress, swap.interopAddress, swap.Symbol, amount)
                });
        }

        throw new OracleException($"unknown transaction for {platformName}.{chainName} : {hash}");
    }

    protected override InteropTransactionData PullTransactionFromPlatform(string platformName, string chainName, Hash hash)
    {
        throw new System.NotImplementedException();
    }

    protected override BigInteger PullFee(Timestamp time, string platform)
    {
        return UnitConversion.ToBigInteger(0.1m, DomainSettings.FiatTokenDecimals);
    }

    protected override decimal PullPrice(Timestamp time, string baseSymbol)
    {
        if (_tokenPrices.ContainsKey(baseSymbol))
        {
            return _tokenPrices[baseSymbol];
        }

        var price = GetDefaultPriceForToken(baseSymbol);                                                                                         
        return price;
    }

    public void UpdatePrice(string symbol, decimal price)
    {
        _tokenPrices[symbol] = price;
    }
    
    private static decimal GetDefaultPriceForToken(string symbol)
    {
        // some dummy values, only really used in the test suite ...                     
        // TODO, remove that, use random data, oracle prices are not fixed in the real world....
        decimal price;
        switch (symbol)
        {
            case "SOUL":
                price = 100;
                break;
            case "KCAL":
                price = 20;
                break;
            case "NEO":
                price = 2000;
                break;
            case "GAS":
                price = 500;
                break;
            case "ETH":
                price = 40000;
                break;
            case "BTC":
                price = 800000;
                break;
            case "COOL":
                price = 300;
                break;
            case "PETH":
                price = 10000;
                break;
            case "PBNB":
                price = 3000;
                break;
            case "PNEO":
                price = 500;
                break;
            case "PGAS":
                price = 50;
                break;
            default:
                price = 0;
                break;
        }

        price /= 1000m;
        return price;
    }


    //protected override decimal PullPrice(Timestamp time, string baseSymbol)
    //{
    //    decimal price;
    //    Random random = new Random();
    //    price = (decimal) Math.Round((random.NextDouble() / 100), 3);
    //    if (price == 0)
    //        price = 1;
    //    return price;
    //}

    protected override InteropNFT PullPlatformNFT(string platformName, string symbol, BigInteger tokenID)
    {
        throw new System.NotImplementedException();
    }
}
