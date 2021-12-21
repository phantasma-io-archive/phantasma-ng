﻿using System;
using System.Net.Http;
using System.Text.Json;
using Phantasma.Shared.Utils;
using Serilog.Core;

namespace Phantasma.Spook.Oracles
{
    public static class CryptoCompareUtils
    {
        
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, string APIKey, PricerSupportedToken[] supportedTokens, Logger logger)
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
                logger.Error($"Error while trying to query {baseticker} price from CryptoCompare API: {errorMsg}");
                return 0;
            }
        }
    }

}
