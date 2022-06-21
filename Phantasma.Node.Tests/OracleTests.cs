using Microsoft.Extensions.Configuration;
using Phantasma.Core;
using Phantasma.Infrastructure;
using Phantasma.Node.Oracles;
using Serilog;
using Serilog.Events;
using Shouldly;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Phantasma.Node.Tests
{   
    public class OracleTests 
    {
        PricerSupportedToken[] tokens = new PricerSupportedToken[]
        {
            new PricerSupportedToken {
                ticker = "SOUL",
                coingeckoId = "phantasma",
                cryptocompareId = "SOUL"
            },
            new PricerSupportedToken {
                ticker = "KCAL",
                coingeckoId = "phantasma-energy",
                cryptocompareId = "KCAL"
            },
            new PricerSupportedToken {
                ticker = "NEO",
                coingeckoId = "neo",
                cryptocompareId = "NEO"
            },
            new PricerSupportedToken {
                ticker = "GAS",
                coingeckoId = "gas",
                cryptocompareId = "GAS"
            },
           new PricerSupportedToken  {
                ticker = "USDT",
                coingeckoId = "tether",
                cryptocompareId = "USDT"
            },
            new PricerSupportedToken {
                ticker = "ETH",
                coingeckoId = "ethereum",
                cryptocompareId = "ETH"
            },
            new PricerSupportedToken {
                ticker = "DAI",
                coingeckoId = "dai",
                cryptocompareId = "DAI"
            },
            new PricerSupportedToken {
                ticker = "DYT",
                coingeckoId = "dynamite",
                cryptocompareId = "DYT"
            },
            new PricerSupportedToken {
                ticker = "DANK",
                coingeckoId = "mu-dank",
                cryptocompareId = "DANK"
            },
            new PricerSupportedToken {
                ticker = "GOATI",
                coingeckoId = "GOATI",
                cryptocompareId = "GOATI"
            },
            new PricerSupportedToken {
                ticker = "USDC",
                coingeckoId = "usd-coin",
                cryptocompareId = "USDC"
            },
            new PricerSupportedToken {
                ticker = "BNB",
                coingeckoId = "binancecoin",
                cryptocompareId = "BNB"
            },
            new PricerSupportedToken {
                ticker = "BUSD",
                coingeckoId = "binance-usd",
                cryptocompareId = "BNB"
            }
        };

        [Fact]
        public async Task StakingTokenPriceShouldNotBeZero()
        {
            var rate = await CoinGeckoUtils.GetCoinRate("NEO", "USD", tokens);
            rate.ShouldNotBe(0);
        }
    }
}