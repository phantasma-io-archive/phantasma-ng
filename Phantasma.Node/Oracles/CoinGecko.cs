using System;
using System.Linq;
using System.Text.Json;
using Org.BouncyCastle.Asn1.Cms;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Serilog;

namespace Phantasma.Node.Oracles
{
    public static class CoinGeckoUtils
    {
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, PricerSupportedToken[] supportedTokens)
        {
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

        public static decimal GetFromHistory(Timestamp time, string baseSymbol, string quoteSymbol, string baseticker)
        {
            var dateTime = (DateTime)time;
            var date = $"{dateTime.Day}-{dateTime.Month}-{dateTime.Year}";
            var url = $"https://api.coingecko.com/api/v3/coins/{baseticker}/history?date={date}&localization=en";
            //var url = $"https://api.coingecko.com/api/v3/simple/price?ids={baseticker}&vs_currencies={quoteSymbol}";

            try
            {
                var response = RequestUtils.Request<JsonDocument>(RequestType.GET, url, out var _);

                if (response == null)
                    return 0;

                if (!response.RootElement.TryGetProperty("market_data", out var marketData))
                {
                    return 0;
                }

                if (!marketData.TryGetProperty("current_price", out var currentPrice))
                {
                    return 0;
                }

                if (currentPrice.TryGetProperty(quoteSymbol.ToLower(), out var priceProperty))
                {
                    return priceProperty.GetDecimal();
                }

                return 0;
            }
            catch (Exception e)
            {
                return 0;
            }
        }

        public static decimal GetFromMarketChartRange(Timestamp time, string baseSymbol, string quoteSymbol, string baseticker)
        {
            var initialTime = (DateTime)time;
            DateTimeOffset utcTime2 = initialTime;
            Timestamp from = utcTime2.Date;
            var url = $"https://api.coingecko.com/api/v3/coins/{baseticker}/market_chart/range?vs_currency={quoteSymbol}&from={from.Value}&to={time.Value}";
            
            try
            {
                var response = RequestUtils.Request<JsonDocument>(RequestType.GET, url, out var _);

                if (response == null)
                    return 0;

                if(!response.RootElement.TryGetProperty("prices", out var pricesData))
                {
                    return 0;
                }

                var array = pricesData.EnumerateArray().First();
                var price = array.EnumerateArray().Last().GetDecimal();
                
                return price;

                return 0;
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                Log.Error($"Error while trying to query {baseticker} price from CoinGecko API: {errorMsg}");
                return 0;
            }
        }

        public static decimal GetCoinRateWithTime(Timestamp time, string baseSymbol, string quoteSymbol,
            PricerSupportedToken[] supportedTokens)
        {
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
            
            return GetFromMarketChartRange(time, baseSymbol, quoteSymbol, baseticker);
            //var url = $"https://api.coingecko.com/api/v3/coins/{baseticker}/history?date={date}&localization=en";
            //var url = $"https://api.coingecko.com/api/v3/simple/price?ids={baseticker}&vs_currencies={quoteSymbol}";
        }
    }
}
