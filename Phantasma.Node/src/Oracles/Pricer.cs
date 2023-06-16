using System;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;
using Phantasma.Node.Configuration;
using Serilog;

namespace Phantasma.Node.Oracles
{
    public static class Pricer
    {
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, string cryptoCompApiKey, bool cgEnabled, PricerSupportedToken[] supportedTokens)
        {
            try
            {
                Decimal cGeckoPrice = 0;
                Decimal cComparePrice = 0;

                if (string.IsNullOrEmpty(cryptoCompApiKey))
                {
                    cComparePrice = CryptoCompareUtils.GetCoinRate(baseSymbol, quoteSymbol, cryptoCompApiKey, supportedTokens);
                }

                if (cgEnabled)
                {
                    cGeckoPrice = CoinGeckoUtils.GetCoinRate(baseSymbol, quoteSymbol, supportedTokens);
                }

                if ((cGeckoPrice > 0) && (cComparePrice > 0))
                {
                    return ((cGeckoPrice + cComparePrice) / 2);
                }
                if((cGeckoPrice > 0) && (cComparePrice <= 0))
                {
                    return (cGeckoPrice);
                }
                if ((cComparePrice > 0) && (cGeckoPrice <= 0))
                {
                    return (cComparePrice);
                }
                return 0;

            }
            catch (Exception ex)
            {
                var errorMsg = ex.ToString();
                Log.Error($"Pricer error: {errorMsg}");
                return 0;
            }
        }

        public static decimal GetCoinRateWithTime(Timestamp time, string baseSymbol, string quoteSymbol,
            string cryptoCompApiKey, bool cgEnabled, PricerSupportedToken[] supportedTokens)
        {
            try
            {
                Decimal cGeckoPrice = 0;
                Decimal cComparePrice = 0;

                if (time < new Timestamp(1644570000))
                {
                    if (string.IsNullOrEmpty(cryptoCompApiKey))
                    {
                        cComparePrice = CryptoCompareUtils.GetCoinRate(baseSymbol, quoteSymbol, cryptoCompApiKey, supportedTokens);
                    }
                    
                    if (cgEnabled)
                    {
                        cGeckoPrice = CoinGeckoUtils.GetCoinRate(baseSymbol, quoteSymbol, supportedTokens);
                        if (baseSymbol == DomainSettings.FuelTokenSymbol)
                        {
                            return cGeckoPrice;
                        }
                    }
                    
                    if ((cGeckoPrice > 0) && (cComparePrice > 0))
                    {
                        return ((cGeckoPrice + cComparePrice) / 2);
                    }
                    if((cGeckoPrice > 0) && (cComparePrice <= 0))
                    {
                        return (cGeckoPrice);
                    }
                    if ((cComparePrice > 0) && (cGeckoPrice <= 0))
                    {
                        return (cComparePrice);
                    }
                }
                else
                {
                    if (cgEnabled)
                    {
                        cGeckoPrice = CoinGeckoUtils.GetCoinRateWithTime(time, baseSymbol, quoteSymbol, supportedTokens);
                        return cGeckoPrice;
                    }
                }
                
                return 0;

            }
            catch (Exception ex)
            {
                var errorMsg = ex.ToString();
                Log.Error($"Pricer error: {errorMsg}");
                return 0;
            }
        }
    }
}
