using System.Collections.Generic;
using System.Numerics;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Node.Configuration;
using Serilog;

namespace Phantasma.Node.Interop;

public abstract class ChainSwapper
{
    public readonly string PlatformName;
    public readonly TokenSwapper Swapper;
    public readonly string LocalAddress;
    public readonly string WIF;
    public IOracleReader OracleReader => Swapper.OracleReader;

    protected ChainSwapper(TokenSwapper swapper, string platformName)
    {
        Swapper = swapper;

        this.WIF = Settings.Instance.GetInteropWif(swapper.SwapKeys, platformName);
        this.PlatformName = platformName;
        this.LocalAddress = swapper.FindAddress(platformName);

        // for testing with mainnet swap address
        //this.LocalAddress = "AbFdbvacCeBrncvwYnPEtfKqyr5KU9SWAU"; //swapper.FindAddress(platformName);

        if (string.IsNullOrEmpty(LocalAddress))
        {
            throw new SwapException($"Invalid address for {platformName} swaps");
        }

        var localKeys = GetAvailableAddress(this.WIF);
        if (localKeys == LocalAddress)
        {
            Log.Information($"Listening for {platformName} swaps at address {LocalAddress.ToLower()}");
        }
        else
        {
            Log.Error($"Expected {platformName} keys to {LocalAddress}, instead got keys to {localKeys}");
        }
    }

    protected abstract string GetAvailableAddress(string wif);
    public abstract IEnumerable<PendingSwap> Update();
    public abstract void ResyncBlock(System.Numerics.BigInteger blockId);

    internal abstract Hash SettleSwap(Hash sourceHash, Address destination, IToken token, BigInteger amount);
    internal abstract Hash VerifyExternalTx(Hash sourceHash, string txStr);
}
