using System;
using System.Text.Json;
using Phantasma.Shared.Utils;
using Serilog;

namespace Phantasma.Spook.Oracles
{
    public static class CoinGeckoUtils
    {
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, PricerSupportedToken[] supportedTokens)
        {

            string json;
            string baseticker = "";

            foreach (var token in supportedTokens)
            {
                if(token.ticker == baseSymbol)
                {
                    baseticker = token.coingeckoId;
                    break;
                }
            }

            if (String.IsNullOrEmpty(baseticker))
                return 0;

            // hack for goati price .10
            if (baseticker == "GOATI")
            {
                var price = 0.10m;
                return price;
            }

            var url = $"https://api.coingecko.com/api/v3/simple/price?ids={baseticker}&vs_currencies={quoteSymbol}";

            try
            {
                var response = RequestUtils.Request<JsonDocument>(RequestType.GET, url, out var _);

                if (response == null)
                    return 0;

                if(!response.RootElement.TryGetProperty(baseticker, out var baseTickerProperty))
                {
                    return 0;
                }
                if(baseTickerProperty.TryGetProperty(quoteSymbol.ToLower(), out var priceProperty))
                {
                    return priceProperty.GetDecimal();
                }
                return 0;
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                Log.Error($"Error while trying to query {baseticker} price from CoinGecko API: {errorMsg}");
                return 0;
            }
        }
    }
}
