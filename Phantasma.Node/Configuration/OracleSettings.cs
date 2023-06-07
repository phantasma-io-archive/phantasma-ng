using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Configuration;
using Phantasma.Infrastructure.API;

namespace Phantasma.Node;

public class OracleSettings
{
    public List<string> EthRpcNodes { get; }
    public List<FeeUrl> EthFeeURLs { get; }
    public List<FeeUrl> BscFeeURLs { get; }
    public bool PricerCoinGeckoEnabled { get; } = true;
    public List<PricerSupportedToken> PricerSupportedTokens { get; }
    public PlatformSettings[] SwapPlatforms { get; private set; }
    public string CryptoCompareAPIKey { get; }
    public string Swaps { get; }
    public string PhantasmaInteropHeight { get; } = "0";
    public string BSCInteropHeight { get; private set; }
    public string EthInteropHeight { get; }
    public string EthWif { get; }
    public uint EthConfirmations { get; }
    public uint EthGasLimit { get; }

    public OracleSettings(IConfigurationSection section)
    {
        this.EthRpcNodes = section.GetSection("eth.rpc.nodes").AsEnumerable().Where(x => x.Value != null).Select(x => x.Value).ToList();

        this.EthFeeURLs = section.GetSection("eth.fee.urls").Get<FeeUrl[]>().ToList();

        this.PricerCoinGeckoEnabled = section.GetValueEx<bool>("pricer.coingecko.enabled");
        this.PricerSupportedTokens = section.GetSection("pricer.supportedtokens").Get<PricerSupportedToken[]>().ToList();

        this.EthConfirmations = section.GetValueEx<UInt32>("eth.block.confirmations");
        this.EthGasLimit = section.GetValueEx<UInt32>("eth.gas.limit");
        this.CryptoCompareAPIKey = section.GetString("crypto.compare.key");
        this.Swaps = section.GetString("swap.platforms");
        this.PhantasmaInteropHeight = section.GetString("phantasma.interop.height");
        this.EthInteropHeight = section.GetString("eth.interop.height");
        this.EthWif = section.GetString("eth.wif");
        if (string.IsNullOrEmpty(this.EthWif))
        {
            this.EthWif = null;
        }
        
        GetSwapPlatforms(section);
    }
    
    /// <summary>
    /// Get and configure the Swap Platforms
    /// </summary>
    /// <param name="section"></param>
    /// <exception cref="Exception"></exception>
    private void GetSwapPlatforms(IConfigurationSection section)
    {
        var swapNode = section.GetSection("swap.platforms");
        if (swapNode == null)
        {
            throw new Exception("Config is missing swaps.platform entry");
        }

        int index = 0;
        this.SwapPlatforms = new PlatformSettings[swapNode.GetChildren().Count()];
        foreach (var node in swapNode.GetChildren())
        {
            var platformName = node.GetString("name");
            Console.WriteLine("name: " + platformName);

            var platform = new PlatformSettings();
            SwapPlatforms[index] = platform;

            if (!Enum.TryParse<SwapPlatformChain>(platformName, true, out platform.Chain))
            {
                throw new Exception($"Unknown swap platform entry in config: '{platformName}'");
            }


            var temp = node.GetString("height", "0");
            if (!BigInteger.TryParse(temp, out platform.InteropHeight))
            {
                throw new Exception($"Invalid interop swap height '{temp}' for platform '{platformName}'");
            }

            var rpcNodes = node.GetSection("rpc.nodes");
            if (rpcNodes == null)
            {
                throw new Exception($"Config is missing rpc.nodes for platform '{platformName}'");
            }

            platform.RpcNodes = rpcNodes.GetChildren().Select(x => x.Value).ToArray();
            
            var interop = node.GetSection("interop");
            if (interop == null)
            {
                throw new Exception($"Config is missing interop for platform '{platformName}'");
            }

            var fuel = node.GetString("fuel");
            if (string.IsNullOrEmpty(fuel))
            {
                throw new Exception($"Config is missing fuel for platform '{platformName}'");
            }

            platform.Fuel = fuel;

            var tokens = node.GetSection("tokens");
            if (tokens == null)
            {
                throw new Exception($"Config is missing tokens for platform '{platformName}'");
            }
            
            platform.Tokens = tokens.GetChildren().Select(x => x.Value).ToArray();

            index++;
        }
    }
        
    public PlatformSettings GetPlatformSettings(SwapPlatformChain chain)
    {
        return SwapPlatforms.FirstOrDefault(x => x.Chain == chain);
    }
}
