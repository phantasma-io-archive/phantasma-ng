using System;
using System.Text.Json;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Serilog;

namespace Phantasma.Node.Oracles
{
    public static class CryptoCompareUtils
    {
        
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, string APIKey, PricerSupportedToken[] supportedTokens)
        {

            string baseticker = "";

            foreach (var token in supportedTokens)
            {
                if (token.ticker == baseSymbol)
                {
                    baseticker = token.cryptocompareId;
                    break;
                }
            }

            if (String.IsNullOrEmpty(baseticker))
                return 0;

            var url = $"https://min-api.cryptocompare.com/data/price?fsym={baseticker}&tsyms={quoteSymbol}&api_key={APIKey}";

            try
            {
                var response = RequestUtils.Request<JsonDocument>(RequestType.GET, url, out var _);

                if(response == null)
                    return 0;

                if (response.RootElement.TryGetProperty(quoteSymbol, out var priceProperty))
                {
                    return priceProperty.GetDecimal();
                }

                return 0;
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                Log.Error($"Error while trying to query {baseticker} price from CryptoCompare API: {errorMsg}");
                return 0;
            }
        }
        
        public static decimal GetCoinRateWithTime(Timestamp time, string baseSymbol, string quoteSymbol, string APIKey, PricerSupportedToken[] supportedTokens)
        {

            string baseticker = "";

            foreach (var token in supportedTokens)
            {
                if (token.ticker == baseSymbol)
                {
                    baseticker = token.cryptocompareId;
                    break;
                }
            }

            if (String.IsNullOrEmpty(baseticker))
                return 0;

            var url = $"https://min-api.cryptocompare.com/data/price?fsym={baseticker}&tsyms={quoteSymbol}&api_key={APIKey}";

            try
            {
                var response = RequestUtils.Request<JsonDocument>(RequestType.GET, url, out var _);

                if(response == null)
                    return 0;

                if (response.RootElement.TryGetProperty(quoteSymbol, out var priceProperty))
                {
                    return priceProperty.GetDecimal();
                }

                return 0;
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                Log.Error($"Error while trying to query {baseticker} price from CryptoCompare API: {errorMsg}");
                return 0;
            }
        }

    }

}
