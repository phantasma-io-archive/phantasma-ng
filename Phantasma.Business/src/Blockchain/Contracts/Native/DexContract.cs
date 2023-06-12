using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Exchange;
using Phantasma.Core.Domain.Contract.Exchange.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed partial class ExchangeContract : NativeContract
    {
        #region Dex
        internal BigInteger _DEXversion;
        public BigInteger GetDexVerion() => GetDexVersion();


        /// <summary>
        /// Returns the current DEX version
        /// </summary>
        /// <returns></returns>
        public BigInteger GetDexVersion()
        {
            return _DEXversion;
        }

        /// <summary>
        /// Check if a Token is supported
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public bool IsSupportedToken(string symbol)
        {
            if (!Runtime.TokenExists(symbol))
            {
                return false;
            }

            if (symbol == DomainSettings.StakingTokenSymbol)
            {
                return true;
            }

            if (symbol == DomainSettings.FuelTokenSymbol)
            {
                return true;
            }

            var info = Runtime.GetToken(symbol);
            return info.IsFungible() && info.Flags.HasFlag(TokenFlags.Transferable);
        }

        /// <summary>
        /// To deposit tokens on the contract
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol"></param>
        /// <param name="amount"></param>
        public void DepositTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "address must be user address");

            Runtime.Expect(IsSupportedToken(symbol), "token is unsupported");

            var info = Runtime.GetToken(symbol);
            var unitAmount = UnitConversion.GetUnitValue(info.Decimals);
            Runtime.Expect(amount >= unitAmount, "invalid amount");

            Runtime.TransferTokens(symbol, from, Address, amount);
        }

        /// <summary>
        /// Swap tokens Pool version (DEX Version)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        public void SwapTokens(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(_DEXversion >= 1, "call migrateV3 first");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount > 0, $"invalid amount, need to be higher than 0 | {amount}");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(IsSupportedToken(fromSymbol), "source token is unsupported");

            var fromBalance = Runtime.GetBalance(fromSymbol, from);
            Runtime.Expect(fromBalance > 0, $"not enough {fromSymbol} balance");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(IsSupportedToken(toSymbol), "destination token is unsupported");

            var total = GetRate(fromSymbol, toSymbol, amount);
            Runtime.Expect(total > 0, "amount to swap needs to be larger than zero");

            var route = GetPoolBestRoute(fromSymbol, toSymbol, amount);

            if (!PoolExists(fromSymbol, toSymbol) || route.Route.Length > 1)
            {
                Runtime.Expect(route.Route.Length > 0, "No route found");
                BigInteger amountToSwap = amount;
                BigInteger rate = amount;
                for (int i = 0; i < route.Route.Length; i++)
                {
                    rate =  GetRate(route.Route[i].FromSymbol, route.Route[i].ToSymbol, amountToSwap);
                    SwapTokens(from, route.Route[i].FromSymbol, route.Route[i].ToSymbol, amountToSwap);
                    amountToSwap = rate;
                }
                return;
            }

            // Validate Pools
            Runtime.Expect(PoolExists(fromSymbol, toSymbol), $"Pool {fromSymbol}/{toSymbol} doesn't exist.");

            Pool pool = GetPool(fromSymbol, toSymbol);

            BigInteger toPotBalance = 0;
            if (pool.Symbol0 == fromSymbol)
                toPotBalance = pool.Amount1;
            else
                toPotBalance = pool.Amount0;

            if (toPotBalance < total && toSymbol == DomainSettings.FuelTokenSymbol)
            {
                var gasAddress = GetAddressForNative(NativeContractKind.Gas);
                var gasBalance = Runtime.GetBalance(toSymbol, gasAddress);
                if (gasBalance >= total)
                {
                    Runtime.TransferTokens(toSymbol, gasAddress, Address, total);
                    toPotBalance = total;
                }
            }

            var toSymbolDecimalsInfo = Runtime.GetToken(toSymbol);
            var toSymbolDecimals = Math.Pow(10, toSymbolDecimalsInfo.Decimals);
            var fromSymbolDecimalsInfo = Runtime.GetToken(fromSymbol);
            var fromSymbolDecimals = Math.Pow(10, fromSymbolDecimalsInfo.Decimals);
            Runtime.Expect(toPotBalance >= total, $"insufficient balance in pot, have {(double)toPotBalance / toSymbolDecimals} {toSymbol} in pot, need {(double)total / toSymbolDecimals} {toSymbol}, have {(double)fromBalance / fromSymbolDecimals} {fromSymbol} to convert from");

            bool canBeTraded = false;
            if (pool.Symbol0 == fromSymbol)
                canBeTraded = ValidateTrade(amount, total, pool, true);
            else
                canBeTraded = ValidateTrade(total, amount, pool);

            Runtime.Expect(canBeTraded, $"The trade is not valid.");

            Runtime.TransferTokens(fromSymbol, from, Address, amount);
            Runtime.TransferTokens(toSymbol, Address, from, total);

            // Trading volume
            UpdateTradingVolume(pool, fromSymbol, amount);

            // Handle Fees
            // Fees are always based on SOUL traded.
            BigInteger totalFees = 0;
            BigInteger feeForUsers = 0;
            BigInteger feeForOwner = 0;
            // total *  ExhcangeDexDefaultFee / 100; To calculate the total fees in the pool
            totalFees = total * ExhcangeDexDefaultFee / 100;
            feeForUsers = totalFees * 100 / ExchangeDexDefaultPoolPercent;
            feeForOwner = totalFees * 100 / ExchangeDexDefaultGovernancePercent;

            // Total * 100 - 3 (ExchangeDexDefaultFee) / 100 - This is used to remove the fees amount that goes into the pool
            var totalAmountToRemove = total * (100 - ExhcangeDexDefaultFee) / 100;
            if (pool.Symbol0 == fromSymbol)
            {
                pool.Amount0 += amount;
                pool.Amount1 -= totalAmountToRemove;
                pool.FeesForOwnerSymbol0 += feeForOwner;
                pool.FeesForUsersSymbol0 += feeForUsers;
            }
            else
            {
                pool.Amount0 -= totalAmountToRemove;
                pool.Amount1 += amount;
                pool.FeesForUsersSymbol1 += feeForUsers;
                pool.FeesForOwnerSymbol1 += feeForOwner;
            }

            DistributeFee(feeForUsers, pool, fromSymbol);

            // Save Pool
            _pools.Set($"{pool.Symbol0}_{pool.Symbol1}", pool);
        }

        /// <summary>
        /// To swap tokens in reverse
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="total"></param>
        public void SwapReverse(Address from, string fromSymbol, string toSymbol, BigInteger total)
        {
            var amount = GetRate(toSymbol, fromSymbol, total);
            Runtime.Expect(amount > 0, $"cannot reverse swap {fromSymbol}");
            SwapTokens(from, fromSymbol, toSymbol, amount);
        }

        /// <summary>
        /// Swap Fee -> Method used to convert a Symbol into KCAL, Using Pools
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="feeAmount"></param>
        public void SwapFee(Address from, string fromSymbol, BigInteger feeAmount)
        {
            Runtime.Expect(_DEXversion >= 1, "call migrateToV3 first");
            Runtime.Expect(fromSymbol != DomainSettings.FuelTokenSymbol, "cannot swap fuel token");
            var feeSymbol = DomainSettings.FuelTokenSymbol;

            // Need to remove the fees
            var token = Runtime.GetToken(fromSymbol);
            BigInteger minAmount;

            var feeBalance = Runtime.GetBalance(feeSymbol, from);
            //feeAmount -= UnitConversion.ConvertDecimals(feeBalance, DomainSettings.FuelTokenDecimals, token.Decimals);
            if (feeAmount <= 0)
            {
                return;
            }

            // different tokens have different decimals, so we need to make sure a certain minimum amount is swapped
            if (token.Decimals == 0)
            {
                minAmount = 1;
            }
            else
            {
                var diff = DomainSettings.FuelTokenDecimals - token.Decimals;
                if (diff > 0)
                {
                    minAmount = BigInteger.Pow(10, diff);
                }
                else
                {
                    minAmount = 1 * BigInteger.Pow(10, token.Decimals);
                }
            }

            if (!PoolIsReal(fromSymbol, feeSymbol))
            {
                var rate = GetRate(fromSymbol, "SOUL", feeAmount);
                SwapTokens(from, fromSymbol, "SOUL", feeAmount);
                SwapTokens(from, "SOUL", feeSymbol, rate);
                return;
            }
            else
            {
                var amountInOtherSymbol = GetRate(feeSymbol, fromSymbol, feeAmount);
                var amountIKCAL = feeAmount;
                Console.WriteLine($"AmountOther: {amountInOtherSymbol} | feeAmount:{feeAmount} | feeBalance:{feeBalance} | amountOfKcal: {amountIKCAL}");

                if (amountInOtherSymbol < minAmount)
                {
                    amountInOtherSymbol = minAmount;
                }

                // round up
                //amountInOtherSymbol++;

                SwapTokens(from, fromSymbol, feeSymbol, amountInOtherSymbol);
            }

            var finalFeeBalance = Runtime.GetBalance(feeSymbol, from);
            Runtime.Expect(finalFeeBalance >= feeBalance, $"something went wrong in swapFee finalFeeBalance: {finalFeeBalance} feeBalance: {feeBalance}");
        }

        /// <summary>
        /// Swap Fiat
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="worth"></param>
        public void SwapFiat(Address from, string fromSymbol, string toSymbol, BigInteger worth)
        {
            var amount = GetRate(DomainSettings.FiatTokenSymbol, fromSymbol, worth);

            var token = Runtime.GetToken(fromSymbol);
            if (token.Decimals == 0 && amount < 1)
            {
                amount = 1;
            }
            else
            {
                Runtime.Expect(amount > 0, $"cannot swap {fromSymbol} based on fiat quote");
            }

            SwapTokens(from, fromSymbol, toSymbol, amount);
        }

        /// <summary>
        /// Get the Rate for the trade (with fees included)
        /// </summary>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount">Amount of fromSymbol to Swap</param>
        /// <returns></returns>
        public BigInteger GetRate(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(IsSupportedToken(fromSymbol), "unsupported from symbol");
            Runtime.Expect(IsSupportedToken(toSymbol), $"unsupported to symbol -> {toSymbol}");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(toInfo.IsFungible(), "must be fungible");

            //Runtime.Expect(PoolExists(fromSymbol, toSymbol), $"Pool {fromSymbol}/{toSymbol} doesn't exist.");
            var route = GetPoolBestRoute(fromSymbol, toSymbol, amount);

            if (!PoolExists(fromSymbol, toSymbol) || route.Route.Length > 1)
            {
                Runtime.Expect(route.Route.Length > 0, "No route found");
                BigInteger finalRate = amount;
                for (int i = 0; i < route.Route.Length; i++)
                {
                    finalRate = GetRate(route.Route[i].FromSymbol, route.Route[i].ToSymbol, finalRate);
                }
                return finalRate;
            }

            BigInteger rate = 0;

            Pool pool = GetPool(fromSymbol, toSymbol);
            BigInteger tokenAmount = 0;

            //BigInteger power = 0;
            //BigInteger rateForSwap = 0;

            BigInteger feeAmount = pool.FeeRatio;

            bool canBeTraded = false;

            // dy = y * 0.997 * dx /  ( x + 0.997 * dx )
            if (pool.Symbol0 == fromSymbol)
            {
                tokenAmount = pool.Amount1 * (1 - feeAmount / 100) * amount / (pool.Amount0 + (1 - feeAmount / 100) * amount);
                if (toInfo.Decimals == 0)
                    tokenAmount = (int)tokenAmount;
                canBeTraded = ValidateTrade(amount, tokenAmount, pool, true);
                //power = (BigInteger)Math.Pow((long)(pool.Amount0 - amount), 2);
                //rateForSwap = pool.Amount0 * pool.Amount1 * 10000000000 / power;
            }
            else
            {

                tokenAmount = pool.Amount0 * (1 - feeAmount / 100) * amount / (pool.Amount1 + (1 - feeAmount / 100) * amount);
                Console.WriteLine($"Test-> {tokenAmount}");
                if (toInfo.Decimals == 0)
                    tokenAmount = (int)tokenAmount;
                canBeTraded = ValidateTrade(tokenAmount, amount, pool, false);
                //canBeTraded = false;
                //power = (BigInteger)Math.Pow((long)(pool.Amount1 + amount), 2);
                //rateForSwap =  pool.Amount0 * pool.Amount1 * 10000000000 / power;
            }

            Console.WriteLine($"Test-> {tokenAmount}");

            rate = tokenAmount;
            Runtime.Expect(canBeTraded, "Can't be traded, the trade is not valid.");
            Runtime.Expect(rate >= 0, "invalid swap rate");

            return rate;
        }

        /// <summary>
        /// Method to just upgrade to new version not setupPools
        /// </summary>
        public void Migrate()
        {
            Runtime.Expect(_DEXversion == 0, "Migration failed, wrong version");

            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            Runtime.Expect(existsLP, "LP token doesn't exist!");

            Runtime.Expect(Runtime.PreviousContext.Name.ToLower() == "lp", "Migration failed, wrong context");
            Runtime.Expect(Runtime.IsWitness(GetAddressFromContractName("LP")), "Only LP can migrate");

            _DEXversion = 1;
        }

        /// <summary>
        /// Method use to Migrate to the new SwapMechanism
        /// </summary>
        public void MigrateToV3()
        {
            Runtime.Expect(_DEXversion == 0, "Migration failed, wrong version");

            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            Runtime.Expect(existsLP, "LP token doesn't exist!");

            Runtime.Expect(Runtime.PreviousContext.Name.ToLower() == "lp", "Migration failed, wrong context");
            Runtime.Expect(Runtime.IsWitness(GetAddressFromContractName("LP")), "Only LP can migrate");

            // check how much SOUL we have here
            var soulTotal = Runtime.GetBalance(DomainSettings.StakingTokenSymbol, Address);

            // creates a new pool for SOUL and every asset that has a balance in v2
            var symbols = Runtime.GetTokens();

            var tokens = new Dictionary<string, BigInteger>();

            // fetch all fungible tokens with balance > 0
            foreach (var symbol in symbols)
            {
                if (symbol == DomainSettings.StakingTokenSymbol)
                {
                    continue;
                }

                var info = Runtime.GetToken(symbol);
                if (info.IsFungible())
                {
                    var balance = Runtime.GetBalance(symbol, Address);

                    if (balance > 0)
                    {
                        tokens[symbol] = balance;
                    }
                }
            }

            // sort tokens by estimated SOUL value, from low to high
            var sortedTokens = tokens.Select(x =>
                    new KeyValuePair<string, BigInteger>(x.Key, Runtime.GetTokenQuote(x.Key, DomainSettings.StakingTokenSymbol, x.Value)))
                //GetRate(x.Key, DomainSettings.StakingTokenSymbol, x.Value)
                .OrderBy(x => x.Value)
                .Select(x => x.Key)
                .ToArray();

            /*var sortedTokens = tokens.Select(x =>
                    new KeyValuePair<string, BigInteger>(x.Key, GetTokenQuote(x.Key)))
                //GetRate(x.Key, DomainSettings.StakingTokenSymbol, x.Value)
                .OrderBy(x => x.Value)
                .Select(x => x.Key)
                .ToArray();*/


            // Calculate the Percent to each Pool
            var tokensPrice = new Dictionary<string, BigInteger>();
            var soulPrice = Runtime.GetTokenQuote(DomainSettings.StakingTokenSymbol, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
            var soulTotalPrice = soulPrice * UnitConversion.ConvertDecimals(soulTotal, DomainSettings.StakingTokenDecimals, DomainSettings.FiatTokenDecimals);
            BigInteger otherTokensTotalValue = 0;
            BigInteger totalTokenAmount = 0;
            BigInteger amount = 0;
            BigInteger tokenPrice = 0;
            BigInteger tokenRatio = 0;
            BigInteger totalPrice = 0;
            BigInteger tokenAmount = 0;
            BigInteger percent = 0;
            BigInteger soulAmount = 0;
            IToken tokenInfo;

            foreach (var symbol in sortedTokens)
            {
                tokenInfo = Runtime.GetToken(symbol);

                amount = UnitConversion.ConvertDecimals(tokens[symbol], tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
                tokenPrice = Runtime.GetTokenQuote(symbol, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, tokenInfo.Decimals));

                //Console.WriteLine($"{symbol} price {tokenPrice}$  .{tokenInfo.Decimals}  { amount}x{tokenPrice} :{ amount * tokenPrice} -> {UnitConversion.ToDecimal(tokenPrice, DomainSettings.FiatTokenDecimals)}");
                tokensPrice[symbol] = tokenPrice;
                otherTokensTotalValue += amount * tokenPrice;
            }

            // Create Pools based on that percent and on the availableSOUL and on token ratio
            BigInteger totalSOULUsed = 0;
            _DEXversion = 1;

            if (otherTokensTotalValue < soulTotalPrice)
            {
                foreach (var symbol in sortedTokens)
                {
                    tokenInfo = Runtime.GetToken(symbol);
                    totalTokenAmount = UnitConversion.ConvertDecimals(tokens[symbol], tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
                    amount = tokens[symbol];
                    soulAmount = tokensPrice[symbol] * totalTokenAmount / soulPrice;
                    //Console.WriteLine($"TokenInfo |-> .{tokenInfo.Decimals} | ${tokensPrice[symbol]} | {tokens[symbol]} {symbol} | Converted {amount} {symbol} ");
                    //Console.WriteLine($"TradeValues |-> {percent}% | {tokenAmount} | {soulAmount}/{soulTotal}\n");
                    //Console.WriteLine($"{symbol} |-> .{tokenInfo.Decimals} | ${UnitConversion.ToDecimal(tokensPrice[symbol], DomainSettings.FiatTokenDecimals)} | {UnitConversion.ToDecimal(tokens[symbol], tokenInfo.Decimals)} {symbol} | Converted {UnitConversion.ToDecimal(amount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"{symbol} Ratio |-> {tokenRatio}% | {UnitConversion.ToDecimal(amount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"SOUL |-> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)}/{UnitConversion.ToDecimal(soulTotal, DomainSettings.StakingTokenDecimals)} | {soulPrice} | {soulTotalPrice}");
                    //Console.WriteLine($"TradeValues |-> {percent}% | {amount} | {soulAmount}/{soulTotal} -> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL");
                    //Console.WriteLine($"Trade {UnitConversion.ToDecimal(amount, tokenInfo.Decimals)} {symbol} for {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL\n");
                    totalSOULUsed += soulAmount;
                    CreatePool(Address, DomainSettings.StakingTokenSymbol, soulAmount, symbol, amount);
                }
            }
            else
            {
                // With Price Ratio
                foreach (var symbol in sortedTokens)
                {
                    tokenInfo = Runtime.GetToken(symbol);
                    totalTokenAmount = UnitConversion.ConvertDecimals(tokens[symbol], tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
                    amount = 0;
                    percent = tokensPrice[symbol] * totalTokenAmount * 100 / otherTokensTotalValue;
                    soulAmount = UnitConversion.ConvertDecimals(soulTotal * percent / 100, DomainSettings.FiatTokenDecimals, DomainSettings.StakingTokenDecimals);

                    tokenRatio = tokensPrice[symbol] / soulPrice;
                    if (tokenRatio != 0)
                        tokenAmount = UnitConversion.ConvertDecimals(soulAmount / tokenRatio, DomainSettings.FiatTokenDecimals, tokenInfo.Decimals);
                    else
                    {
                        tokenRatio = soulPrice / tokensPrice[symbol];
                        tokenAmount = UnitConversion.ConvertDecimals(soulAmount * tokenRatio, DomainSettings.FiatTokenDecimals, tokenInfo.Decimals);
                    }

                    //Console.WriteLine($"{symbol} |-> .{tokenInfo.Decimals} | ${UnitConversion.ToDecimal(tokensPrice[symbol], DomainSettings.FiatTokenDecimals)} | {UnitConversion.ToDecimal(tokens[symbol], tokenInfo.Decimals)} {symbol} | Converted {UnitConversion.ToDecimal(tokenAmount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"{symbol} Ratio |-> {tokenRatio}% | {UnitConversion.ToDecimal(tokenAmount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"SOUL |-> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)}/{UnitConversion.ToDecimal(soulTotal, DomainSettings.StakingTokenDecimals)} | {soulPrice} | {soulTotalPrice}");
                    //Console.WriteLine($"TradeValues |-> {percent}% | {tokenAmount} | {soulAmount}/{soulTotal} -> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL");
                    //Console.WriteLine($"Trade {UnitConversion.ToDecimal(tokenAmount, tokenInfo.Decimals)} {symbol} for {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL\n");
                    totalSOULUsed += soulAmount;
                    Runtime.Expect(soulAmount <= soulTotal, $"SOUL higher than total... {soulAmount}/{soulTotal}");
                    CreatePool(Address, DomainSettings.StakingTokenSymbol, soulAmount, symbol, tokenAmount);
                    Runtime.TransferTokens(symbol, Address, GetAddressForNative(NativeContractKind.Swap), tokens[symbol] - tokenAmount);
                }
            }

            Runtime.Expect(totalSOULUsed <= soulTotal, "Used more than it has...");

            // return the left overs
            Runtime.TransferTokens(DomainSettings.StakingTokenSymbol, Address, GetAddressForNative(NativeContractKind.Swap), soulTotal - totalSOULUsed);
        }

        #region DEXify
        public const string ExhcangeDexMinimumFeeTag = "exchange.dex.minimum.fee";
        public const string ExhcangeDexDefaultFeeTag = "exchange.dex.default.fee";
        public static readonly BigInteger ExhcangeDexMinimumFee = 1; // 0.01% - for total fees of the tx
        public static readonly BigInteger ExhcangeDexDefaultFee = 3; // 0.03% - for total fees of the tx

        public const string ExchangeDexPoolPercentTag = "exchange.dex.pool.percent";
        public static readonly BigInteger ExchangeDexDefaultPoolPercent = 75; // 75% of the tx's goes to the LP Providers

        public const string ExchangeDexGovernancePercentTag = "exchange.dex.governance.percent";
        public static readonly BigInteger ExchangeDexDefaultGovernancePercent = 25; // 75% of the tx's goes to the LP Providers

        // value in "per thousands"
        private const int DEXSeriesID = 0;
        internal StorageMap _pools; // <string, Pool> |-> string : $"symbol0_symbol1" |-> Pool : key to the list
        internal StorageList _pool_symbols; // <string> |-> string : $"symbol0_symbol1"
        internal StorageList _pool_routes;
        //internal StorageMap _lp_tokens; // <string, BigInteger>
        internal StorageMap _lp_holders; // <string, storage_list<LPHolderInfo>> |-> string : $"symbol0_symbol1" |-> LPHolderInfo[] : key to the list 
        internal StorageMap _trading_volume; // <string, stoage_map<uint,TradingVolume>> |-> string : $"symbol0_symbol1" |-> TradingVolume[] : key to the list 

        //Runtime.GetGovernanceValue(ValidatorSlotsTag);

        #region Trading Volume
        /// <summary>
        /// Get the storga map of the tradingvolume
        /// </summary>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private StorageMap GetTradingVolume(string symbol0, string symbol1)
        {
            string key = $"{symbol0}_{symbol1}";
            if (!_trading_volume.ContainsKey(key))
            {
                StorageMap newStorage = new StorageMap();
                _trading_volume.Set(key, newStorage);
            }

            var _tradingList = _trading_volume.Get<string, StorageMap>(key);
            return _tradingList;
        }

        // Year -> Math.floor(((time % 31556926) % 2629743) / 86400)
        // Month -> Math.floor(((time % 31556926) % 2629743) / 86400)
        // Day -> Math.floor(((time % 31556926) % 2629743) / 86400)
        public TradingVolume GetTradingVolumeToday(string symbol0, string symbol1)
        {
            if ( Runtime.ProtocolVersion >= 14) Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");
            var tradingMap = GetTradingVolume(symbol0, symbol1);
            var today = DateTime.Today.Date;
            var todayTimestamp = (Timestamp)today;

            TradingVolume tempTrading = new TradingVolume(symbol0, symbol0, today.ToShortDateString(), 0, 0);

            if (tradingMap.ContainsKey(todayTimestamp))
                return tradingMap.Get<Timestamp, TradingVolume>(todayTimestamp);
            return tempTrading;
        }

        /// <summary>
        /// Get all the trading volumes for that Pool
        /// </summary>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        public TradingVolume[] GetTradingVolumes(string symbol0, string symbol1)
        {
            if ( Runtime.ProtocolVersion >= 14) Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");
            var tradingMap = GetTradingVolume(symbol0, symbol1);
            return tradingMap.AllValues<TradingVolume>();
        }

        /// <summary>
        /// Update the trading volume
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="Amount">Always in SOUL AMOUNT!</param>
        private void UpdateTradingVolume(Pool pool, string fromSymbol, BigInteger Amount)
        {
            var today = DateTime.Today.Date;
            var todayTimestamp = (Timestamp)today;
            StorageMap tradingMap = GetTradingVolume(pool.Symbol0, pool.Symbol1);
            var tradingToday = GetTradingVolumeToday(pool.Symbol0, pool.Symbol1);
            string key = $"{pool.Symbol0}_{pool.Symbol1}";
            if (fromSymbol == pool.Symbol0)
                tradingToday.VolumeSymbol0 += Amount;
            else
                tradingToday.VolumeSymbol1 += Amount;

            tradingMap.Set(todayTimestamp, tradingToday);

            _trading_volume.Set(key, tradingMap);
        }
        #endregion

        #region LP Holder
        /// <summary>
        /// This method is used to generate the key related to the USER NFT ID, to make it easier to fetch.
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns>Get LP Tokens Key</returns>
        private string GetLPTokensKey(Address from, string symbol0, string symbol1)
        {
            return $"{from.Text}_{symbol0}_{symbol1}";
        }

        /// <summary>
        /// Get the Holders AddressList for a Specific Pool
        /// </summary>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private StorageList GetHolderList(string symbol0, string symbol1)
        {
            string key = $"{symbol0}_{symbol1}";
            if (!_lp_holders.ContainsKey(key))
            {
                StorageList newStorage = new StorageList();
                _lp_holders.Set(key, newStorage);
            }

            var _holderList = _lp_holders.Get<string, StorageList>(key);
            return _holderList;
        }

        /// <summary>
        /// This method is used to check if the user already has LP on the pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns>True or False depending if the user has it or not</returns>
        private bool UserHasLP(Address from, string symbol0, string symbol1)
        {
            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");
            // Check if a pool exist (token0 - Token 1) and (token 1 - token 0)
            var nfts = Runtime.GetOwnerships(DomainSettings.LiquidityTokenSymbol, from);
            for (int i = 0; i < nfts.Length; i++)
            {
                var nftID = nfts[i];
                var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
                LPTokenContentROM nftROM = VMObject.FromBytes(nft.ROM).AsStruct<LPTokenContentROM>();
                if (nftROM.Symbol0 == symbol0 && nftROM.Symbol1 == symbol1 || nftROM.Symbol0 == symbol1 && nftROM.Symbol1 == symbol0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get LP Holder by Address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private LPHolderInfo GetLPHolder(Address from, string symbol0, string symbol1)
        {
            var nftID = GetMyNFTID(from, symbol0, symbol1);
            Runtime.Expect(CheckHolderIsThePool(nftID, symbol0, symbol1), "User is not on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo tempHolder = new LPHolderInfo();
            while (index < count)
            {
                tempHolder = holdersList.Get<LPHolderInfo>(index);
                if (tempHolder.NFTID == nftID)
                    return tempHolder;

                index++;
            }
            return tempHolder;
        }

        /// <summary>
        /// Check if the holder is on the pool for the fees.
        /// </summary>
        /// <param name="NFTID"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private bool CheckHolderIsThePool(BigInteger nftID, string symbol0, string symbol1)
        {
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo tempHolder;
            while (index < count)
            {
                tempHolder = holdersList.Get<LPHolderInfo>(index);
                if (tempHolder.NFTID == nftID)
                    return true;

                index++;
            }
            return false;
        }

        /// <summary>
        /// Add to LP Holder for that Pool
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void AddToLPHolders(Address from, BigInteger NFTID, string symbol0, string symbol1)
        {
            Runtime.Expect(!CheckHolderIsThePool(NFTID, symbol0, symbol1), "User is already on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var lpHolderInfo = new LPHolderInfo(NFTID, 0, 0, 0, 0);
            holdersList.Add(lpHolderInfo);
            _lp_holders.Set($"{symbol0}_{symbol1}", holdersList);
        }

        /// <summary>
        /// Update LP Holder for that specific pool
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void UpdateLPHolders(LPHolderInfo holder, string symbol0, string symbol1)
        {
            Runtime.Expect(CheckHolderIsThePool(holder.NFTID, symbol0, symbol1), "User is not on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo tempHolder = new LPHolderInfo();

            while (index < count)
            {
                tempHolder = holdersList.Get<LPHolderInfo>(index);
                if (tempHolder.NFTID == holder.NFTID)
                {
                    holdersList.Replace(index, holder);
                    _lp_holders.Set($"{symbol0}_{symbol1}", holdersList);
                    break;
                }
                index++;
            }
        }

        /// <summary>
        /// Remove From the LP Holder for that specific pool
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void RemoveFromLPHolders(Address from, string symbol0, string symbol1)
        {
            var nftID = GetMyNFTID(from, symbol0, symbol1);
            Runtime.Expect(CheckHolderIsThePool(nftID, symbol0, symbol1), "User is not on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo lpHolderInfo = new LPHolderInfo();

            while (index < count)
            {
                lpHolderInfo = holdersList.Get<LPHolderInfo>(index);
                if (lpHolderInfo.NFTID == nftID)
                {
                    holdersList.RemoveAt(index);
                    _lp_holders.Set($"{symbol0}_{symbol1}", holdersList);
                    break;
                }
                index++;
            }
        }

        /// <summary>
        /// This method is to add the NFT ID to the list of NFT in that pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="NFTID">NFT ID</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        private void AddToLPTokens(Address from, BigInteger NFTID, string symbol0, string symbol1)
        {
            AddToLPHolders(from, NFTID, symbol0, symbol1);
        }

        /// <summary>
        /// Update User 
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <param name="claimedAmountSymbol0"></param>
        /// <param name="claimedAmountSymbol1"></param>
        private void UpdateUserLPToken(Address from, string symbol0, string symbol1, BigInteger claimedAmountSymbol0, BigInteger claimedAmountSymbol1)
        {
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");
            Pool pool = GetPool(symbol0, symbol1);
            Runtime.Expect(UserHasLP(from, pool.Symbol0, pool.Symbol1), $"User doesn't have LP");
            var nftID = GetMyNFTID(from, pool.Symbol0, pool.Symbol1);
            var ram = GetMyPoolRAM(from, pool.Symbol0, pool.Symbol1);
            ram.ClaimedFeesSymbol0 += claimedAmountSymbol0;
            ram.ClaimedFeesSymbol1 += claimedAmountSymbol1;
            Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(ram).AsByteArray());
        }


        /// <summary>
        /// This method is used to Remove user From LP Tokens
        /// </summary>
        /// <param name="from"></param>
        /// <param name="NFTID"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void RemoveFromLPTokens(Address from, BigInteger NFTID, string symbol0, string symbol1)
        {
            RemoveFromLPHolders(from, symbol0, symbol1);
        }
        #endregion

        #region Pool Related
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entrySymbol"></param>
        /// <param name="endSymbol"></param>
        /// <returns></returns>
        public PoolRoute GetPoolRoute(string entrySymbol, string endSymbol)
        {
            Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");
            if (PoolExists(entrySymbol, endSymbol)) return new PoolRoute(entrySymbol, endSymbol, new PoolRouteItem[]{new PoolRouteItem(entrySymbol, endSymbol, 0, 0)});
            var poolsSymbols = _pools.AllKeys<string>();
            var graph = BuildGraph(poolsSymbols);
            var path = FindFastestRoute(graph, entrySymbol, endSymbol);
            if (path == null) return new PoolRoute();
            PoolRouteItem[] poolRouteItems = new PoolRouteItem[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                var symbols = path[i].Split('_');
                var pool = GetPool(symbols[0], symbols[1]);
                poolRouteItems[i] = new PoolRouteItem(symbols[0], symbols[1], 0, 0);
            }
            PoolRoute poolRoute = new PoolRoute(entrySymbol, endSymbol, poolRouteItems);
            return poolRoute;
        }
        
        public PoolRoute GetPoolBestRoute(string entrySymbol, string endSymbol, BigInteger amount)
        {
            Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");
            var poolsSymbols = _pools.AllKeys<string>();
            var graph = BuildGraph(poolsSymbols);
            var path = FindBestRoute(graph, entrySymbol, endSymbol, amount);
            if (path == null) return new PoolRoute();
            PoolRouteItem[] poolRouteItems = new PoolRouteItem[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                var symbols = path[i].Split('_');
                var pool = GetPool(symbols[0], symbols[1]);
                poolRouteItems[i] = new PoolRouteItem(symbols[0], symbols[1], 0, 0);
            }
            PoolRoute poolRoute = new PoolRoute(entrySymbol, endSymbol, poolRouteItems);
            return poolRoute;
        }
        
        internal Dictionary<string, List<string>> BuildGraph(string[] pairs)
        {
            var graph = new Dictionary<string, List<string>>();

            foreach (var pair in pairs)
            {
                var tokens = pair.Split('_');
                if (!graph.ContainsKey(tokens[0]))
                {
                    graph[tokens[0]] = new List<string>();
                }

                if (!graph.ContainsKey(tokens[1]))
                {
                    graph[tokens[1]] = new List<string>();
                }

                graph[tokens[0]].Add(tokens[1]);
                graph[tokens[1]].Add(tokens[0]);
            }

            return graph;
        }
        
        internal List<string> FindBestRoute(Dictionary<string, List<string>> graph, string start, string end, BigInteger amount)
        {
            var allRoutes = new List<List<string>>();
            FindAllRoutes(graph, start, end, new List<string>(), new HashSet<string>(), allRoutes);

            List<string> bestRoute = null;
            decimal bestRouteImpact = decimal.MaxValue;

            foreach (var route in allRoutes)
            {
                decimal routeImpact = CalculateTotalPriceImpact(route, amount);

                if (routeImpact < bestRouteImpact)
                {
                    bestRoute = route;
                    bestRouteImpact = routeImpact;
                }
            }

            return bestRoute != null ? ConvertRouteToPairs(bestRoute) : Enumerable.Empty<string>().ToList();
        }
        
        internal void FindAllRoutes(Dictionary<string, List<string>> graph, string current, string end, List<string> path, HashSet<string> visited, List<List<string>> routes)
        {
            path.Add(current);
            visited.Add(current);

            if (current == end)
            {
                routes.Add(new List<string>(path));
            }
            else
            {
                foreach (var neighbor in graph[current])
                {
                    if (!visited.Contains(neighbor))
                    {
                        FindAllRoutes(graph, neighbor, end, path, visited, routes);
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            visited.Remove(current);
        }
        
        internal List<string> FindFastestRoute(Dictionary<string, List<string>> graph, string start, string end)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<List<string>>();
            queue.Enqueue(new List<string> { start });

            while (queue.Count > 0)
            {
                var currentPath = queue.Dequeue();
                var currentNode = currentPath[^1];

                if (currentNode == end)
                {
                    return ConvertRouteToPairs(currentPath);
                }

                if (!visited.Contains(currentNode))
                {
                    visited.Add(currentNode);

                    foreach (var neighbor in graph[currentNode])
                    {
                        var newPath = new List<string>(currentPath) { neighbor };
                        queue.Enqueue(newPath);
                    }
                }
            }
            
            return Enumerable.Empty<string>().ToList();
        }
        
        internal List<string> ConvertRouteToPairs(List<string> route)
        {
            var pairRoute = new List<string>();

            for (int i = 0; i < route.Count - 1; i++)
            {
                pairRoute.Add($"{route[i]}_{route[i + 1]}");
            }

            return pairRoute;
        }
        
        internal decimal CalculateTotalPriceImpact(List<string> route, BigInteger amount)
        {
            decimal totalPriceImpact = 0;

            for (int i = 0; i < route.Count - 1; i++)
            {
                string pair = $"{route[i]}_{route[i + 1]}";
                totalPriceImpact += GetPriceImpact(route[i], route[i + 1], amount);
            }

            return totalPriceImpact;
        }

        internal decimal GetPriceImpact(string symbol0, string symbol1, BigInteger amount)
        {
            Runtime.Expect(PoolExists(symbol0, symbol1), "Pool doesn't exist.");
            var pool = GetPool(symbol0, symbol1);
            BigInteger amount0 = 0;
            BigInteger amount1 = 0;
            BigInteger amountReceive = 0;
            BigInteger poolAmount = 0;
            IToken token0 = Runtime.GetToken(symbol0);
            IToken token1 = Runtime.GetToken(symbol1);
            BigInteger ratio = UnitConversion.ConvertDecimals(pool.Amount0, token0.Decimals, DomainSettings.MAX_TOKEN_DECIMALS) *UnitConversion.ConvertDecimals(pool.Amount1, token1.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
            
            if (PoolIsReal(symbol0, symbol1))
            {
                amount0 = UnitConversion.ConvertDecimals(pool.Amount0, token0.Decimals, DomainSettings.MAX_TOKEN_DECIMALS) + UnitConversion.ConvertDecimals(amount, token0.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
                amount1 = UnitConversion.ConvertDecimals(ratio / amount0, DomainSettings.MAX_TOKEN_DECIMALS,
                    token1.Decimals);
                amountReceive = pool.Amount1 - amount1;
                poolAmount = pool.Amount1;
            }
            else
            {
                amount1 = UnitConversion.ConvertDecimals(pool.Amount1, token1.Decimals, DomainSettings.MAX_TOKEN_DECIMALS) + UnitConversion.ConvertDecimals(amount, token1.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
                amount0 = UnitConversion.ConvertDecimals(ratio / amount1, DomainSettings.MAX_TOKEN_DECIMALS,
                    token0.Decimals);
                amountReceive = pool.Amount0 - amount0;
                poolAmount = pool.Amount0;
            }
            
            decimal result = (decimal) amountReceive * 100 / (decimal) poolAmount;
            return result;
        }
        
        /// <summary>
        /// This method is used to check if the pool is Real or Virtual.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns></returns>
        private bool PoolIsReal(string symbol0, string symbol1)
        {
            return symbol0 == DomainSettings.StakingTokenSymbol || symbol1 == DomainSettings.StakingTokenSymbol;
        }

        /// <summary>
        /// Get NFT ID
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        public BigInteger GetMyNFTID(Address from, string symbol0, string symbol1)
        {
            if(Runtime.ProtocolVersion >= 14) Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");
            var nfts = Runtime.GetOwnerships(DomainSettings.LiquidityTokenSymbol, from);
            BigInteger id = 0;
            for (int i = 0; i < nfts.Length; i++)
            {
                var nftID = nfts[i];
                var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
                LPTokenContentROM nftROM = VMObject.FromBytes(nft.ROM).AsStruct<LPTokenContentROM>();
                if (nftROM.Symbol0 == symbol0 && nftROM.Symbol1 == symbol1)
                {
                    id = nftID;
                    break;
                }
            }

            return id;
        }

        /// <summary>
        /// Get Pool RAM
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        public LPTokenContentRAM GetMyPoolRAM(Address from, string symbol0, string symbol1)
        {
            if(Runtime.ProtocolVersion >= 14) Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");
            Pool pool = GetPool(symbol0, symbol1);
            Runtime.Expect(UserHasLP(from, pool.Symbol0, pool.Symbol1), $"User doesn't have LP");
            var nfts = Runtime.GetOwnerships(DomainSettings.LiquidityTokenSymbol, from);
            LPTokenContentRAM ram = new LPTokenContentRAM();
            for (int i = 0; i < nfts.Length; i++)
            {
                var nftID = nfts[i];
                var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
                LPTokenContentROM nftROM = VMObject.FromBytes(nft.ROM).AsStruct<LPTokenContentROM>();
                LPTokenContentRAM nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();
                if (nftROM.Symbol0 == symbol0 && nftROM.Symbol1 == symbol1)
                {
                    ram = nftRAM;
                    break;
                }
            }

            return ram;
        }

        /// <summary>
        /// This method is used to get all the pools.
        /// </summary>
        /// <returns>Array of Pools</returns>
        public Pool[] GetPools()
        {
            Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");
            return _pools.AllValues<Pool>();
        }

        /// <summary>
        /// This method is used to get a specific pool.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns>Pool</returns>
        public Pool GetPool(string symbol0, string symbol1)
        {
            if(Runtime.ProtocolVersion >= 14) Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            if (_pools.ContainsKey($"{symbol0}_{symbol1}"))
            {
                return _pools.Get<string, Pool>($"{symbol0}_{symbol1}");
            }

            if (_pools.ContainsKey($"{symbol1}_{symbol0}"))
            {
                return _pools.Get<string, Pool>($"{symbol1}_{symbol0}");
            }

            return _pools.Get<string, Pool>($"{symbol0}_{symbol1}");
        }

        /// <summary>
        /// This method is used to check if a pool exists or not.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2ndToken</param>
        /// <returns>True or false</returns>
        private bool PoolExists(string symbol0, string symbol1)
        {
            // Check if the tokens exist
            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            // Check if a pool exist (token0 - Token 1) and (token 1 - token 0)
            if (_pools.ContainsKey($"{symbol0}_{symbol1}") || _pools.ContainsKey($"{symbol1}_{symbol0}"))
            {
                return true;
            }

            return false;
        }
        

        #region Pool Creation
        /*public void CreatePoolV1(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            Runtime.Expect(_DEXversion >= 1, "call migrateV3 first");

            // Check the if the input is valid
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 >= 0 && amount1 >= 0, "invalid amount 0");
            Runtime.Expect(symbol0 != symbol1, "Cannot create a pool with the same symbols");

            // Check if pool exists
            Runtime.Expect(!PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            var symbol0Price = Runtime.GetTokenQuote(symbol0, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, token0Info.Decimals));
            var symbol1Price = Runtime.GetTokenQuote(symbol1, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, token1Info.Decimals)); //GetTokenQuote(symbol1)

            BigInteger sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            decimal tradeRatio = 0;
            decimal tradeRatioAmount = 0;

            if (symbol0Price == 0 || symbol1Price == 0)
            {
                // Use own ratio
                Runtime.Expect(amount0 != 0 && amount1 != 0, "Amount must be different from 0, if there's no symbol price.");
                tradeRatio = GetAmountRatio(amount0, token0Info, amount1, token1Info);
            }
            else
            {
                // Both amounts set, the user created the ration when creating the pool
                if (amount0 != 0 && amount1 != 0)
                {
                    tradeRatio = GetAmountRatio(amount0, token0Info, amount1, token1Info);
                }
                else
                {
                    // Check ratio based on Token Price
                    tradeRatio = decimal.Round(UnitConversion.ToDecimal(symbol0Price, DomainSettings.FiatTokenDecimals) / UnitConversion.ToDecimal(symbol1Price, DomainSettings.FiatTokenDecimals), DomainSettings.MAX_TOKEN_DECIMALS / 2, MidpointRounding.AwayFromZero);

                    if (amount0 == 0)
                    {
                        amount0 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.FiatTokenDecimals) / tradeRatio, token0Info.Decimals);
                    }
                    else if (amount1 == 0)
                    {
                        amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals) / tradeRatio, token1Info.Decimals);
                    }

                }

                sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
                sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            }

            Runtime.Expect(tradeRatio > 0, $"TradeRatio > 0 | {tradeRatio} > 0");

            Runtime.Expect(ValidateRatio(sameDecimalsAmount0, sameDecimalsAmount1, tradeRatio), $"ratio is not true. {tradeRatio}, new {sameDecimalsAmount0} {sameDecimalsAmount1} {sameDecimalsAmount0 / sameDecimalsAmount1} {amount0 / amount1}");

            var symbol0Balance = Runtime.GetBalance(symbol0, from);
            Runtime.Expect(symbol0Balance >= amount0, $"not enough {symbol0} balance, you need {amount0}");
            var symbol1Balance = Runtime.GetBalance(symbol1, from);
            Runtime.Expect(symbol1Balance >= amount1, $"not enough {symbol1} balance, you need {amount1}");


            BigInteger feeRatio = amount0 * ExhcangeDexDefaultFee / 1000;
            feeRatio = ExhcangeDexDefaultFee;

            // Get the token address
            // Token0 Address
            Address token0Address = TokenUtils.GetContractAddress(symbol0);

            // Token1 Address
            Address token1Address = TokenUtils.GetContractAddress(symbol1);

            BigInteger TLP = Sqrt(sameDecimalsAmount0 * sameDecimalsAmount1) * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);

            // Create the pool
            Pool pool = new Pool(symbol0, symbol1, token0Address.Text, token1Address.Text, amount0, amount1, feeRatio, TLP);

            _pools.Set($"{symbol0}_{symbol1}", pool);

            // Give LP Token to the address
            LPTokenContentROM nftROM = new LPTokenContentROM(pool.Symbol0, pool.Symbol1, Runtime.GenerateUID());
            LPTokenContentRAM nftRAM = new LPTokenContentRAM(amount0, amount1, TLP);

            // Mint Token
            var nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, Address, from, VMObject.FromStruct(nftROM).AsByteArray(), VMObject.FromStruct(nftRAM).AsByteArray(), DEXSeriesID);
            Runtime.TransferTokens(pool.Symbol0, from, Address, amount0);
            Runtime.TransferTokens(pool.Symbol1, from, Address, amount1);
            AddToLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
        }*/
        
        /// <summary>
        /// This method is Used to create a Pool.
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void CreatePool(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            ValidatePoolCreation(from, symbol0, amount0, symbol1, amount1);

            var token0Info = Runtime.GetToken(symbol0);
            var token1Info = Runtime.GetToken(symbol1);

            var sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            var sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            // Determine the trade ratio between the tokens
            decimal tradeRatio = CalculateTradeRatio(symbol0, ref amount0, token0Info, symbol1, ref amount1, token1Info, ref sameDecimalsAmount0, ref sameDecimalsAmount1);

            // Calculate the total liquidity for the pool
            BigInteger totalLiquidity = CalculateTotalLiquidity(sameDecimalsAmount0, sameDecimalsAmount1);

            // Create the pool and store it
            Pool pool = InitializePool(symbol0, symbol1, amount0, amount1, totalLiquidity);
            _pools.Set($"{symbol0}_{symbol1}", pool);
            _pool_symbols.Add($"{symbol0}_{symbol1}");

            // Mint LP tokens and transfer the tokens to the pool
            MintLPTokensAndTransferToPool(from, pool.Symbol0, amount0, pool.Symbol1, amount1, totalLiquidity);
        }
        
        private void ValidatePoolCreation(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            Runtime.Expect(_DEXversion >= 1, "call migrateV3 first");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 >= 0, "invalid amount 0");
            Runtime.Expect(amount1 >= 0, "invalid amount 1");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount, both amounts can't be 0");
            Runtime.Expect(symbol0 != symbol1, "Cannot create a pool with the same symbols");

            Runtime.Expect(!PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");

            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");
        }
        
        private decimal CalculateTradeRatio(string symbol0, ref BigInteger amount0, IToken token0Info, string symbol1, ref BigInteger amount1, IToken token1Info, ref BigInteger sameDecimalsAmount0, ref BigInteger sameDecimalsAmount1)
        {
            var symbol0Price = Runtime.GetTokenQuote(symbol0, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, token0Info.Decimals));
            var symbol1Price = Runtime.GetTokenQuote(symbol1, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, token1Info.Decimals));

            decimal tradeRatio = 0;

            if (symbol0Price == 0 || symbol1Price == 0)
            {
                Runtime.Expect(amount0 != 0 && amount1 != 0, "Amount must be different from 0, if there's no symbol price.");
                tradeRatio = GetAmountRatio(amount0, token0Info, amount1, token1Info);
            }
            else
            {
                tradeRatio = decimal.Round(UnitConversion.ToDecimal(symbol0Price, DomainSettings.FiatTokenDecimals) / UnitConversion.ToDecimal(symbol1Price, DomainSettings.FiatTokenDecimals), DomainSettings.MAX_TOKEN_DECIMALS / 2, MidpointRounding.AwayFromZero);

                if ( amount0 != 0 && amount1 != 0)
                {
                    tradeRatio = GetAmountRatio(amount0, token0Info, amount1, token1Info);
                }
                else
                {
                    tradeRatio = decimal.Round(UnitConversion.ToDecimal(symbol0Price, DomainSettings.FiatTokenDecimals) / UnitConversion.ToDecimal(symbol1Price, DomainSettings.FiatTokenDecimals), DomainSettings.MAX_TOKEN_DECIMALS / 2, MidpointRounding.AwayFromZero);
                    if (amount0 == 0)
                    {
                        amount0 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.FiatTokenDecimals) / tradeRatio, token0Info.Decimals);
                    }
                    else if (amount1 == 0)
                    {
                        amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals) / tradeRatio, token1Info.Decimals);
                    }
                }

                sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
                sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            }

            Runtime.Expect(tradeRatio > 0, $"TradeRatio > 0 | {tradeRatio} > 0");
            Runtime.Expect(ValidateRatio(sameDecimalsAmount0, sameDecimalsAmount1, tradeRatio), $"ratio is not true. {tradeRatio}, new {sameDecimalsAmount0} {sameDecimalsAmount1} {sameDecimalsAmount0 / sameDecimalsAmount1} {amount0 / amount1}");

            return tradeRatio;
        }
        
        private BigInteger CalculateTotalLiquidity(BigInteger sameDecimalsAmount0, BigInteger sameDecimalsAmount1)
        {
            return Sqrt(sameDecimalsAmount0 * sameDecimalsAmount1) * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);
        }
        
        private Pool InitializePool(string symbol0, string symbol1, BigInteger amount0, BigInteger amount1, BigInteger totalLiquidity)
        {
            // Get the token address
            Address token0Address = TokenUtils.GetContractAddress(symbol0);
            Address token1Address = TokenUtils.GetContractAddress(symbol1);

            BigInteger feeRatio = ExhcangeDexDefaultFee;

            // Create the pool
            Pool pool = new Pool(
                symbol0, 
                symbol1,
                token0Address.Text, 
                token1Address.Text,
                amount0, 
                amount1,
                feeRatio,
                totalLiquidity
            );

            return pool;
        }
        
        private void MintLPTokensAndTransferToPool(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1, BigInteger totalLiquidity)
        {
            // Give LP Token to the address
            LPTokenContentROM nftROM = new LPTokenContentROM(symbol0, symbol1, Runtime.GenerateUID());
            LPTokenContentRAM nftRAM = new LPTokenContentRAM(amount0, amount1, totalLiquidity);

            // Mint Token
            var nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, Address, from, VMObject.FromStruct(nftROM).AsByteArray(), VMObject.FromStruct(nftRAM).AsByteArray(), DEXSeriesID);
    
            // Transfer the tokens to the pool
            Runtime.TransferTokens(symbol0, from, Address, amount0);
            Runtime.TransferTokens(symbol1, from, Address, amount1);
    
            // Add to LP tokens
            AddToLPTokens(from, nftID, symbol0, symbol1);
        }
        #endregion
        
        #region Add Liquidity
        /// <summary>
        /// This method is used to Provide Liquidity to the Pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void AddLiquidity(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            ValidateInputs(from, symbol0, amount0, symbol1, amount1);

            var token0Info = Runtime.GetToken(symbol0);
            var token1Info = Runtime.GetToken(symbol1);

            ValidateTokens(token0Info, token1Info);

            if (!PoolExists(symbol0, symbol1))
            {
                CreatePool(from, symbol0, amount0, symbol1, amount1);
                return;
            }

            Pool pool = GetPool(symbol0, symbol1);
            UpdateSymbolsAndTokensIfNeeded(ref symbol0, ref symbol1, ref amount0, ref amount1, ref token0Info, ref token1Info, pool);

            BigInteger sameDecimalsAmount0 = ConvertAmountToMAXTokenDecimals(amount0, token0Info);
            BigInteger sameDecimalsAmount1 = ConvertAmountToMAXTokenDecimals(amount1, token1Info);

            // Something missing 
            decimal poolRatio = GetPoolRatioAmounts(pool, ref amount0, ref amount1, ref  sameDecimalsAmount0, ref sameDecimalsAmount1, token0Info, token1Info);
            
            UpdateAmountsIfNeeded(ref amount0, ref amount1, token0Info, token1Info, sameDecimalsAmount0, sameDecimalsAmount1, poolRatio);

            ValidateAmounts(amount0, amount1, token0Info, token1Info, pool, poolRatio);

            UpdateLiquidity(from, symbol0, symbol1, amount0, amount1, token0Info, token1Info, ref pool);
        }
        
        private void ValidateInputs(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 >= 0, "invalid amount 0");
            Runtime.Expect(amount1 >= 0, "invalid amount 1");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount, both amounts can't be 0");
            Runtime.Expect(symbol0 != symbol1, "Symbols cannot be the same...");
            Runtime.Expect(_DEXversion >= 1, "call migrateV3 first");
        }

        private void ValidateTokens(IToken token0Info, IToken token1Info)
        {
            Runtime.Expect(IsSupportedToken(token0Info.Symbol), "source token is unsupported");
            Runtime.Expect(IsSupportedToken(token1Info.Symbol), "destination token is unsupported");
        }

        private void UpdateSymbolsAndTokensIfNeeded(ref string symbol0, ref string symbol1, ref BigInteger amount0, ref BigInteger amount1, ref IToken token0Info, ref IToken token1Info, Pool pool)
        {
            if (symbol0 != pool.Symbol0)
            {
                symbol0 = pool.Symbol0;
                symbol1 = pool.Symbol1;
                (token0Info, token1Info) = (token1Info, token0Info);
                (amount0, amount1) = (amount1, amount0);
            }
        }

        private BigInteger ConvertAmountToMAXTokenDecimals(BigInteger amount, IToken tokenInfo)
        {
            return UnitConversion.ConvertDecimals(amount, tokenInfo.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
        }
        
        private BigInteger ConvertAmountToStakingTokenDecimals(BigInteger amount, IToken tokenInfo)
        {
            return UnitConversion.ConvertDecimals(amount, tokenInfo.Decimals, DomainSettings.StakingTokenDecimals);
        }
        
        private BigInteger ConvertAmountToFiatTokenDecimals(BigInteger amount, IToken tokenInfo)
        {
            return UnitConversion.ConvertDecimals(amount, tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
        }

        private void UpdateAmountsIfNeeded(ref BigInteger amount0, ref BigInteger amount1, IToken token0Info, IToken token1Info, BigInteger sameDecimalsAmount0, BigInteger sameDecimalsAmount1, decimal poolRatio)
        {
            if (amount0 == 0)
            {
                amount0 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.MAX_TOKEN_DECIMALS) / poolRatio, token0Info.Decimals);
            }
            else if (amount1 == 0)
            {
                amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.MAX_TOKEN_DECIMALS) / poolRatio, token1Info.Decimals);
            }
        }
        
        private decimal GetPoolRatioAmounts(Pool pool, ref BigInteger amount0, ref BigInteger amount1, ref BigInteger sameDecimalsAmount0, ref BigInteger sameDecimalsAmount1, IToken token0Info, IToken token1Info)
        {
            var poolRatio = GetPoolRatio(pool, token0Info, token1Info);
            var tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);
            if (amount0 == 0)
            {
                amount0 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.MAX_TOKEN_DECIMALS) / poolRatio, token0Info.Decimals);
            }
            else if (amount1 == 0)
            {
                amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.MAX_TOKEN_DECIMALS) / poolRatio, token1Info.Decimals);
            }
            
            sameDecimalsAmount0 = ConvertAmountToMAXTokenDecimals(amount0, token0Info);
            sameDecimalsAmount1 = ConvertAmountToMAXTokenDecimals(amount1, token1Info);
            
            if (tradeRatioAmount != poolRatio)
            {
                amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.MAX_TOKEN_DECIMALS) / poolRatio, token1Info.Decimals);

                sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
                sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);

                tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);
            }

            return poolRatio;
        }
        
        private void ValidateAmounts(BigInteger amount0, BigInteger amount1, IToken token0Info, IToken token1Info, Pool pool, decimal poolRatio)
        {
            BigInteger sameDecimalsAmount0 = ConvertAmountToMAXTokenDecimals(amount0, token0Info);
            BigInteger sameDecimalsAmount1 = ConvertAmountToMAXTokenDecimals(amount1, token1Info);

            decimal tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);
            Runtime.Expect(tradeRatioAmount == poolRatio, $"TradeRatio < 0 | {poolRatio} != {tradeRatioAmount}");

            bool isValidRatio = ValidateRatio(sameDecimalsAmount0, sameDecimalsAmount1, poolRatio);
            Runtime.Expect(isValidRatio, $"ratio is not true. {poolRatio}, new {sameDecimalsAmount0} {sameDecimalsAmount1} {sameDecimalsAmount1 / sameDecimalsAmount0} {amount1 / amount0}");
        }

        private void UpdateLPToken(Address from, string symbol0, string symbol1, BigInteger amount0, BigInteger amount1, BigInteger liquidity)
        {
            BigInteger nftID = GetMyNFTID(from, symbol0, symbol1);
            var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
            LPTokenContentRAM nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();

            nftRAM.Amount0 += amount0;
            nftRAM.Amount1 += amount1;
            nftRAM.Liquidity += liquidity;

            Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());
        }
        
        private void MintLPToken(Address from, string symbol0, string symbol1, BigInteger amount0, BigInteger amount1, BigInteger liquidity)
        {
            LPTokenContentROM nftROM = new LPTokenContentROM(symbol0, symbol1, Runtime.GenerateUID());
            LPTokenContentRAM nftRAM = new LPTokenContentRAM(amount0, amount1, liquidity);

            BigInteger nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, Address, from, VMObject.FromStruct(nftROM).AsByteArray(), VMObject.FromStruct(nftRAM).AsByteArray(), DEXSeriesID);
            AddToLPTokens(from, nftID, symbol0, symbol1);
        }

        private void TransferTokensAndUpdatePool(Address from, string symbol0, string symbol1, BigInteger amount0, BigInteger amount1, BigInteger liquidity, ref Pool pool)
        {
            Runtime.TransferTokens(symbol0, from, Address, amount0);
            Runtime.TransferTokens(symbol1, from, Address, amount1);
            pool.Amount0 += amount0;
            pool.Amount1 += amount1;
            pool.TotalLiquidity += liquidity;

            _pools.Set($"{symbol0}_{symbol1}", pool);
        }
        
        private void UpdateLiquidity(Address from, string symbol0, string symbol1, BigInteger amount0, BigInteger amount1, IToken token0Info, IToken token1Info, ref Pool pool)
        {
            BigInteger sameDecimalsAmount0 = ConvertAmountToMAXTokenDecimals(amount0, token0Info);
            BigInteger sameDecimalsAmount1 = ConvertAmountToMAXTokenDecimals(amount1, token1Info);
            BigInteger poolSameDecimalsAmount0 = ConvertAmountToMAXTokenDecimals(pool.Amount0, token0Info);

            BigInteger liquidity = sameDecimalsAmount0 * pool.TotalLiquidity / poolSameDecimalsAmount0;

            if (UserHasLP(from, symbol0, symbol1))
            {
                UpdateLPToken(from, symbol0, symbol1, amount0, amount1, liquidity);
            }
            else
            {
                MintLPToken(from, symbol0, symbol1, amount0, amount1, liquidity);
            }

            TransferTokensAndUpdatePool(from, symbol0, symbol1, amount0, amount1, liquidity, ref pool);
        }
        
        /// <summary>
        /// This method is used to Provide Liquidity to the Pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void AddLiquidityV1(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            // Check input
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 >= 0, "invalid amount 0");
            Runtime.Expect(amount1 >= 0, "invalid amount 1");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount, both amounts can't be 0");
            Runtime.Expect(symbol0 != symbol1, "Symbols cannot be the same...");
            Runtime.Expect(_DEXversion >= 1, "call migrateV3 first");
            //Runtime.Expect(false, "");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            // Check if pool exists
            if (!PoolExists(symbol0, symbol1))
            {
                CreatePool(from, symbol0, amount0, symbol1, amount1);
                return;
            }

            // Get pool
            Pool pool = GetPool(symbol0, symbol1);
            decimal poolRatio = 0;
            decimal tradeRatioAmount = 0;

            // Fix inputs
            if (symbol0 != pool.Symbol0)
            {
                symbol0 = pool.Symbol0;
                symbol1 = pool.Symbol1;
                (token0Info, token1Info) = (token1Info, token0Info);
                (amount0, amount1) = (amount1, amount0);
            }

            // Pool Amounts Same Decimals
            BigInteger poolSameDecimalsAmount0 = UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger poolSameDecimalsAmount1 = UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            BigInteger sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            // Get the Pool Ratio
            poolRatio = GetPoolRatio(pool, token0Info, token1Info);

            // Calculate Amounts if they are 0
            if (amount0 == 0)
            {
                amount0 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.FiatTokenDecimals) / poolRatio, token0Info.Decimals);
            }
            else
            {
                if (amount1 == 0)
                {
                    amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals) / poolRatio, token1Info.Decimals);
                }
            }

            sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.StakingTokenDecimals);
            sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.StakingTokenDecimals);

            // Get Trading Ratio
            tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);

            if (poolRatio == 0)
            {
                poolRatio = tradeRatioAmount;
            }
            else
            {
                //if (tradeRatioAmount == 0)
                //    tradeRatioAmount = poolRatio;

                // If the ratio's differ it means that the amount in is not true to the pool ratio.
                if (tradeRatioAmount != poolRatio)
                {
                    amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals) / poolRatio, token1Info.Decimals);

                    sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
                    sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

                    tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);
                }

                Runtime.Expect(tradeRatioAmount == poolRatio, $"TradeRatio < 0 | {poolRatio} != {tradeRatioAmount}");
            }

            // Validate the Ratio
            Runtime.Expect(ValidateRatio(sameDecimalsAmount0, sameDecimalsAmount1, poolRatio), $"ratio is not true. {poolRatio}, new {sameDecimalsAmount0} {sameDecimalsAmount1} {sameDecimalsAmount1 / sameDecimalsAmount0} {amount1 / amount0}");

            //Console.WriteLine($"ADD: ratio:{poolRatio} | amount0:{amount0} | amount1:{amount1}");
            BigInteger liquidity = 0;
            BigInteger nftID = 0;
            LPTokenContentROM nftROM = new LPTokenContentROM(pool.Symbol0, pool.Symbol1, Runtime.GenerateUID());
            LPTokenContentRAM nftRAM = new LPTokenContentRAM();

            // Check if user has the LP Token and Update the values
            if (UserHasLP(from, pool.Symbol0, pool.Symbol1))
            {
                // Update the NFT VALUES
                nftID = GetMyNFTID(from, symbol0, symbol1);
                var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
                nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();

                // CALCULATE BASED ON THIS lp_amount = (SOUL_USER  * LP_TOTAL )/  SOUL_TOTAL
                // TODO: calculate the amounts according to the ratio...

                liquidity = sameDecimalsAmount0 * pool.TotalLiquidity / poolSameDecimalsAmount0; //* UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);

                nftRAM.Amount0 += amount0;
                nftRAM.Amount1 += amount1;
                nftRAM.Liquidity += liquidity;

                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());
            }
            else
            {
                // MINT NFT and give to the user
                // CALCULATE BASED ON THIS lp_amount = (SOUL_USER  * LP_TOTAL )/  SOUL_TOTAL
                liquidity = sameDecimalsAmount0 * pool.TotalLiquidity / poolSameDecimalsAmount0; //*UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);
                nftRAM = new LPTokenContentRAM(amount0, amount1, liquidity);

                nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, Address, from, VMObject.FromStruct(nftROM).AsByteArray(), VMObject.FromStruct(nftRAM).AsByteArray(), DEXSeriesID);
                AddToLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
            }

            // Update the pool values
            Runtime.TransferTokens(pool.Symbol0, from, Address, amount0);
            Runtime.TransferTokens(pool.Symbol1, from, Address, amount1);
            pool.Amount0 += amount0;
            pool.Amount1 += amount1;
            pool.TotalLiquidity += liquidity;

            _pools.Set($"{pool.Symbol0}_{pool.Symbol1}", pool);
        }
        #endregion

        #region Remove Liquidity
        /// <summary>
        /// This method is used to Remove Liquidity from the pool.
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void RemoveLiquidity(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            ValidateInputs(from,  symbol0,  amount0,  symbol1,  amount1);

            // Check if user has LP Token
            Runtime.Expect(UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var token0Info = Runtime.GetToken(symbol0);
            var token1Info = Runtime.GetToken(symbol1);
            
            ValidateTokens(token0Info, token1Info);
            
            // Get Pool
            Pool pool = GetPool(symbol0, symbol1);

            UpdateSymbolsAndTokensIfNeeded(ref symbol0, ref symbol1, ref amount0, ref amount0, ref token0Info,
                ref token1Info, pool);
            
            BigInteger liquidity = 0;

            // Pool Amounts Same Decimals
            BigInteger poolSameDecimalsAmount0 = UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
            BigInteger poolSameDecimalsAmount1 = UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);

            BigInteger sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
            BigInteger sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);

            // Calculate Amounts
            decimal poolRatio = 0;
            decimal tradeRatioAmount = 0;

            poolRatio = GetPoolRatio(pool, token0Info, token1Info);

            // Calculate Amounts if they are 0
            if (amount0 == 0)
            {
                amount0 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.MAX_TOKEN_DECIMALS) / poolRatio, token0Info.Decimals);
            }
            else if (amount1 == 0)
            {
                amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.MAX_TOKEN_DECIMALS) / poolRatio, token1Info.Decimals);
            }

            // To the same Decimals for ease of calculation
            sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
            sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);

            tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);

            if (poolRatio == 0)
            {
                poolRatio = tradeRatioAmount;
            }
            else
            {
                if (tradeRatioAmount != poolRatio)
                {
                    amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.MAX_TOKEN_DECIMALS) / poolRatio, token1Info.Decimals);

                    sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
                    sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);

                    tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);

                }

                Runtime.Expect(tradeRatioAmount == poolRatio, $"TradeRatio < 0 | {poolRatio} != {tradeRatioAmount}");
            }

            //Console.WriteLine($"pool:{poolRatio} | trade:{tradeRatioAmount} | {amount0} {symbol0} for {amount1} {symbol1}");
            Runtime.Expect(ValidateRatio(sameDecimalsAmount0, sameDecimalsAmount1, poolRatio), $"ratio is not true. {poolRatio}, new {sameDecimalsAmount0} {sameDecimalsAmount1} {sameDecimalsAmount0 / sameDecimalsAmount1} {amount0 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals)}");

            // Update the user NFT
            var nfts = Runtime.GetOwnerships(DomainSettings.LiquidityTokenSymbol, from);

            TokenContent nft;
            var nftID = GetMyNFTID(from, symbol0, symbol1);
            LPTokenContentRAM nftRAM = GetMyPoolRAM(from, symbol0, symbol1);

            BigInteger oldAmount0 = nftRAM.Liquidity * pool.Amount0 / pool.TotalLiquidity;
            BigInteger oldAmount1 = nftRAM.Liquidity * pool.Amount1 / pool.TotalLiquidity;
            BigInteger newAmount0 = oldAmount0 - amount0;
            BigInteger newAmount1 = oldAmount1 - amount1;
            BigInteger oldLP = nftRAM.Liquidity;
            Console.WriteLine($"new LP Division? : {pool.Amount0 - nftRAM.Amount0}");
            Console.WriteLine($"Pool Amount : {pool.Amount0} | NFT -> {nftRAM.Amount0}");
            Console.WriteLine($"Old Amount 0 : {oldAmount0} |  1 -> {oldAmount1}");
            BigInteger division = pool.Amount0 - nftRAM.Amount0;
            BigInteger lp = pool.TotalLiquidity - nftRAM.Liquidity;

            if (pool.Amount0 - nftRAM.Amount0 <= 0)
            {
                division = 1;
            }

            // To make math possible.
            if (lp == 0)
            {
                lp = 1;
            }

            BigInteger newLiquidity = 0;
            if (pool.Amount0 == oldAmount0)
            {
                newLiquidity = Sqrt(newAmount0 * newAmount1);
            }
            else
            {
                newLiquidity = newAmount0 * lp / division;
            }


            Console.WriteLine($"LP Division? : {pool.Amount0}");

            liquidity = amount0 * pool.TotalLiquidity / pool.Amount0;

            Runtime.Expect(nftRAM.Liquidity - liquidity >= 0, "Trying to remove more than you have...");

            Console.WriteLine($"BeforeLP:{nftRAM.Liquidity} - LiquidityToRemove:{liquidity} | FinalLP:{newLiquidity}");


            // If the new amount will be = 0 then burn the NFT
            if (newAmount0 == 0)
            {
                // Claim Fees
                ClaimFees(from, symbol0, symbol1);
                // Change NFT Values
                nftRAM.Liquidity = 0;
                nftRAM.Amount0 = 0;
                nftRAM.Amount1 = 0;
                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());

                // Burn NFT
                Runtime.BurnToken(DomainSettings.LiquidityTokenSymbol, from, nftID);
                RemoveFromLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
                Runtime.TransferTokens(pool.Symbol0, Address, from, oldAmount0);
                Runtime.TransferTokens(pool.Symbol1, Address, from, oldAmount1);
            }
            else
            {
                Runtime.Expect(oldAmount0 - amount0 > 0, $"Lower Amount for symbol {symbol0}. | You have {oldAmount0} {symbol0}, trying to remove {amount0} {symbol0}");
                nftRAM.Amount0 = newAmount0;

                Runtime.Expect(oldAmount1 - amount1 > 0, $"Lower Amount for symbol {symbol1}. | You have {oldAmount1} {symbol1}, trying to remove {amount1} {symbol1}");
                nftRAM.Amount1 = newAmount1;

                Runtime.TransferTokens(symbol0, Address, from, amount0);
                Runtime.TransferTokens(symbol1, Address, from, amount1);

                nftRAM.Liquidity = newLiquidity;

                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());
            }

            // Update the pool values
            if (pool.Amount0 == oldAmount0)
            {
                pool.Amount0 = newAmount0;
                pool.Amount1 = newAmount1;
                pool.TotalLiquidity = newLiquidity;
            }
            else
            {
                pool.Amount0 = pool.Amount0 - oldAmount0 + newAmount0;
                pool.Amount1 = pool.Amount1 - oldAmount1 + newAmount1;
                pool.TotalLiquidity = pool.TotalLiquidity - oldLP + newLiquidity;
            }

            if (pool.Amount0 == 0)
            {
                _pools.Remove($"{pool.Symbol0}_{pool.Symbol1}");
                _pool_symbols.Remove($"{pool.Symbol0}_{pool.Symbol1}");
            }
            else
            {
                _pools.Set($"{pool.Symbol0}_{pool.Symbol1}", pool);
            }
        }
        
        /// <summary>
        /// Calculates the liquidity and amounts to add to the pool
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="token0Info"></param>
        /// <param name="token1Info"></param>
        /// <param name="amount0"></param>
        /// <param name="amount1"></param>
        /// <returns></returns>
        /*private (BigInteger, BigInteger, BigInteger) CalculateLiquidityAndAmounts(Pool pool, TokenInfo token0Info, TokenInfo token1Info, BigInteger amount0, BigInteger amount1)
        {
            BigInteger liquidity = 0;
            decimal poolRatio = GetPoolRatio(pool, token0Info, token1Info);
    
            if (amount0 == 0 || amount1 == 0)
            {
                (amount0, amount1) = CalculateAmounts(pool, token0Info, token1Info, amount0, amount1, poolRatio);
            }

            BigInteger sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
    
            decimal tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);
    
            Runtime.Expect(tradeRatioAmount == poolRatio, $"TradeRatio < 0 | {poolRatio} != {tradeRatioAmount}");
    
            liquidity = CalculateLiquidity(pool, amount0);
    
            return (liquidity, sameDecimalsAmount0, sameDecimalsAmount1);
        }*/
        
        /*private void UpdateUserNFT(Address from, string symbol0, string symbol1, Pool pool, BigInteger amount0, BigInteger amount1, BigInteger sameDecimalsAmount0, BigInteger sameDecimalsAmount1, BigInteger liquidity)
        {
            var nftID = GetMyNFTID(from, symbol0, symbol1);
            LPTokenContentRAM nftRAM = GetMyPoolRAM(from, symbol0, symbol1);

            BigInteger oldAmount0 = nftRAM.Liquidity * pool.Amount0 / pool.TotalLiquidity;
            BigInteger oldAmount1 = nftRAM.Liquidity * pool.Amount1 / pool.TotalLiquidity;
            BigInteger newAmount0 = oldAmount0 - amount0;
            BigInteger newAmount1 = oldAmount1 - amount1;
            BigInteger oldLP = nftRAM.Liquidity;

            BigInteger division = pool.Amount0 - nftRAM.Amount0;
            BigInteger lp = pool.TotalLiquidity - nftRAM.Liquidity;

            if (pool.Amount0 - nftRAM.Amount0 <= 0)
            {
                division = 1;
            }

            if (lp == 0)
            {
                lp = 1;
            }

            BigInteger newLiquidity;
            if (pool.Amount0 == oldAmount0)
            {
                newLiquidity = Sqrt(newAmount0 * newAmount1);
            }
            else
            {
                newLiquidity = newAmount0 * lp / division;
            }

            Runtime.Expect(nftRAM.Liquidity - liquidity >= 0, "Trying to remove more than you have...");

            if (newAmount0 == 0)
            {
                ClaimFees(from, symbol0, symbol1);
                nftRAM.Liquidity = 0;
                nftRAM.Amount0 = 0;
                nftRAM.Amount1 = 0;
                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());

                Runtime.BurnToken(DomainSettings.LiquidityTokenSymbol, from, nftID);
                RemoveFromLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
                Runtime.TransferTokens(pool.Symbol0, Address, from, oldAmount0);
                Runtime.TransferTokens(pool.Symbol1, Address, from, oldAmount1);
            }
            else
            {
                Runtime.Expect(oldAmount0 - amount0 > 0, $"Lower Amount for symbol {symbol0}. | You have {oldAmount0} {symbol0}, trying to remove {amount0} {symbol0}");
                nftRAM.Amount0 = newAmount0;

                Runtime.Expect(oldAmount1 - amount1 > 0, $"Lower Amount for symbol {symbol1}. | You have {oldAmount1} {symbol1}, trying to remove {amount1} {symbol1}");
                nftRAM.Amount1 = newAmount1;

                Runtime.TransferTokens(symbol0, Address, from, amount0);
                Runtime.TransferTokens(symbol1, Address, from, amount1);

                nftRAM.Liquidity = newLiquidity;

                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());
            }

            UpdatePool(pool, oldAmount0, oldAmount1, newAmount0, newAmount1, oldLP, newLiquidity);
        }*/
        #endregion

        #endregion

        #region Checkers
        /// <summary>
        /// Validate Trade
        /// </summary>
        /// <param name="amount0"></param>
        /// <param name="amount1"></param>
        /// <param name="pool"></param>
        /// <param name="isBuying"></param>
        /// <returns></returns>
        private bool ValidateTrade(BigInteger amount0, BigInteger amount1, Pool pool, bool isBuying = false)
        {
            if (isBuying)
            {
                if (pool.Amount0 + amount0 > 0 && pool.Amount1 - amount1 > 0)
                    return true;
            }
            else
            {
                if (pool.Amount0 - amount0 > 0 && pool.Amount1 + amount1 > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Validate Ratio
        /// </summary>
        /// <param name="amount0"></param>
        /// <param name="amount1"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private bool ValidateRatio(BigInteger amount0, BigInteger amount1, decimal ratio)
        {
            decimal sameDecimalsAmount0 = UnitConversion.ToDecimal(amount0, DomainSettings.FiatTokenDecimals);
            decimal sameDecimalsAmount1 = UnitConversion.ToDecimal(amount1, DomainSettings.FiatTokenDecimals);

            if (sameDecimalsAmount1 > 0)
                return decimal.Round(sameDecimalsAmount0 / sameDecimalsAmount1, DomainSettings.MAX_TOKEN_DECIMALS / 2, MidpointRounding.AwayFromZero) == ratio;
            return false;
        }

        /// <summary>
        /// Calculate fee's to LP on the pool
        /// </summary>
        /// <param name="totalFee"></param>
        /// <param name="liquidity"></param>
        /// <param name="totalLiquidity"></param>
        /// <returns></returns>
        private BigInteger CalculateFeeForUser(BigInteger totalFee, BigInteger liquidity, BigInteger totalLiquidity)
        {
            BigInteger feeAmount = liquidity * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS) / totalLiquidity;
            return totalFee * feeAmount / UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);
        }

        /// <summary>
        /// Get the pool ratio
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="token0Info"></param>
        /// <param name="token1Info"></param>
        /// <returns></returns>
        private decimal GetPoolRatio(Pool pool, IToken token0Info, IToken token1Info)
        {
            BigInteger poolSameDecimalsAmount0 = UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
            BigInteger poolSameDecimalsAmount1 = UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
            if (poolSameDecimalsAmount1 > 0)
                return decimal.Round(UnitConversion.ToDecimal(poolSameDecimalsAmount0, DomainSettings.FiatTokenDecimals) / UnitConversion.ToDecimal(poolSameDecimalsAmount1, DomainSettings.FiatTokenDecimals), DomainSettings.MAX_TOKEN_DECIMALS / 2, MidpointRounding.AwayFromZero);
            return 0;
        }

        private decimal GetAmountRatio(BigInteger amount0, IToken token0Info, BigInteger amount1, IToken token1Info)
        {
            BigInteger amount0SameDecimals = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
            BigInteger amount1SameDecimals = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.MAX_TOKEN_DECIMALS);
            if (amount1 > 0)
                return decimal.Round(UnitConversion.ToDecimal(amount0SameDecimals, DomainSettings.MAX_TOKEN_DECIMALS) / UnitConversion.ToDecimal(amount1SameDecimals, DomainSettings.MAX_TOKEN_DECIMALS), DomainSettings.MAX_TOKEN_DECIMALS / 2, MidpointRounding.AwayFromZero);
            return 0;
        }
        #endregion

        #region LP Holder Interactions
        /// <summary>
        /// Distribute Fees
        /// </summary>
        /// <param name="totalFeeAmount"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void DistributeFee(BigInteger totalFeeAmount, Pool pool, string symbolDistribute)
        {
            Runtime.Expect(PoolExists(pool.Symbol0, pool.Symbol1), $"Pool {pool.Symbol0}/{pool.Symbol1} doesn't exist.");
            var holdersList = GetHolderList(pool.Symbol0, pool.Symbol1);
            var index = 0;
            var count = holdersList.Count();
            var feeAmount = totalFeeAmount;
            BigInteger amount = 0;
            BigInteger distributed = 0;
            LPHolderInfo holder = new LPHolderInfo();
            LPTokenContentRAM nftRAM = new LPTokenContentRAM();

            var holderInfo = holdersList.All<LPHolderInfo>();
            foreach (var _holder in holderInfo)
            {
                holder = _holder;
                if (!Runtime.NFTExists(DomainSettings.LiquidityTokenSymbol, holder.NFTID))
                {
                    continue;
                }
                
                var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, holder.NFTID);
                nftRAM = GetMyPoolRAM(nft.CurrentOwner, pool.Symbol0, pool.Symbol1);
                amount = CalculateFeeForUser(totalFeeAmount, nftRAM.Liquidity, pool.TotalLiquidity);
                if (nft.CurrentOwner != Address)
                    Runtime.Expect(amount > 0, $"Amount failed for user: {nft.CurrentOwner}, amount:{amount}, feeAmount:{feeAmount}, feeTotal:{totalFeeAmount}");

                feeAmount -= amount;
                distributed += amount;
                if (pool.Symbol0 == symbolDistribute)
                    holder.UnclaimedSymbol0 += amount;
                else
                    holder.UnclaimedSymbol1 += amount;

                holdersList.Replace(index, holder);
            }

            Runtime.Expect(feeAmount <= 1 || feeAmount == totalFeeAmount || distributed + feeAmount == totalFeeAmount, $"feeAmount {feeAmount} was > than 0, distributed: {distributed}, total fee: {totalFeeAmount}");

            // Update List
            _lp_holders.Set($"{pool.Symbol0}_{pool.Symbol1}", holdersList);
        }

        /// <summary>
        /// Method used to claim fees
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        public void ClaimFees(Address from, string symbol0, string symbol1)
        {
            if ( Runtime.ProtocolVersion >= 14) Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            // Check if user has LP Token
            Runtime.Expect(UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var holder = GetLPHolder(from, symbol0, symbol1);
            var unclaimedAmount0 = holder.UnclaimedSymbol0;
            var unclaimedAmount1 = holder.UnclaimedSymbol1;

            Runtime.TransferTokens(symbol0, Address, from, unclaimedAmount0);
            Runtime.TransferTokens(symbol1, Address, from, unclaimedAmount1);

            holder.ClaimedSymbol0 += unclaimedAmount0;
            holder.ClaimedSymbol1 += unclaimedAmount1;
            holder.UnclaimedSymbol0 = 0;
            holder.UnclaimedSymbol1 = 0;

            // Update LP Holder
            UpdateLPHolders(holder, symbol0, symbol1);

            // Update NFT
            UpdateUserLPToken(from, symbol0, symbol1, unclaimedAmount0, unclaimedAmount1);
        }

        /// <summary>
        /// Get unclaimed fees;
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        public (BigInteger, BigInteger) GetUnclaimedFees(Address from, string symbol0, string symbol1)
        {
            if ( Runtime.ProtocolVersion >= 14) Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");

            // Check if user has LP Token
            Runtime.Expect(UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var holder = GetLPHolder(from, symbol0, symbol1);
            return (holder.UnclaimedSymbol0, holder.UnclaimedSymbol1);
        }
        #endregion

        #region NFT Interactions

        private void DeleteNFT(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1, LPTokenContentRAM nftRAM, BigInteger nftID)
        {
            // Check input
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 >= 0, "invalid amount 0");
            Runtime.Expect(amount1 >= 0, "invalid amount 1");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount, both amounts can't be 0");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            // Get Pool
            Pool pool = GetPool(symbol0, symbol1);
            BigInteger liquidity = 0;

            // Fix inputs
            if (symbol0 != pool.Symbol0)
            {
                symbol0 = pool.Symbol0;
                symbol1 = pool.Symbol1;
                (token0Info, token1Info) = (token1Info, token0Info);
                (amount0, amount1) = (amount1, amount0);
            }

            // Pool Amounts Same Decimals
            BigInteger poolSameDecimalsAmount0 =
                UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger poolSameDecimalsAmount1 =
                UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            BigInteger sameDecimalsAmount0 =
                UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger sameDecimalsAmount1 =
                UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            // Calculate Amounts
            decimal poolRatio = 0;
            decimal tradeRatioAmount = 0;

            poolRatio = GetPoolRatio(pool, token0Info, token1Info);

            // Calculate Amounts if they are 0
            if (amount0 == 0)
            {
                amount0 = UnitConversion.ToBigInteger(
                    UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.FiatTokenDecimals) / poolRatio,
                    token0Info.Decimals);
            }
            else if (amount1 == 0)
            {
                amount1 = UnitConversion.ToBigInteger(
                    UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals) / poolRatio,
                    token1Info.Decimals);
            }

            // To the same Decimals for ease of calculation
            sameDecimalsAmount0 =
                UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            sameDecimalsAmount1 =
                UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);

            if (poolRatio == 0)
            {
                poolRatio = tradeRatioAmount;
            }
            else
            {
                if (tradeRatioAmount != poolRatio)
                {
                    amount1 = UnitConversion.ToBigInteger(
                        UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals) / poolRatio,
                        token1Info.Decimals);

                    sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals,
                        DomainSettings.FiatTokenDecimals);
                    sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals,
                        DomainSettings.FiatTokenDecimals);

                    tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);

                }

                Runtime.Expect(tradeRatioAmount == poolRatio, $"TradeRatio < 0 | {poolRatio} != {tradeRatioAmount}");
            }

            //Console.WriteLine($"pool:{poolRatio} | trade:{tradeRatioAmount} | {amount0} {symbol0} for {amount1} {symbol1}");
            Runtime.Expect(ValidateRatio(sameDecimalsAmount0, sameDecimalsAmount1, poolRatio),
                $"ratio is not true. {poolRatio}, new {sameDecimalsAmount0} {sameDecimalsAmount1} {sameDecimalsAmount0 / sameDecimalsAmount1} {amount0 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals)}");

            // Update the user NFT
            BigInteger oldAmount0 = nftRAM.Liquidity * pool.Amount0 / pool.TotalLiquidity;
            BigInteger oldAmount1 = nftRAM.Liquidity * pool.Amount1 / pool.TotalLiquidity;
            BigInteger newAmount0 = oldAmount0 - amount0;
            BigInteger newAmount1 = oldAmount1 - amount1;
            BigInteger oldLP = nftRAM.Liquidity;
            BigInteger division = pool.Amount0 - nftRAM.Amount0;
            BigInteger lp = pool.TotalLiquidity - nftRAM.Liquidity;

            if (pool.Amount0 - nftRAM.Amount0 <= 0)
            {
                division = 1;
            }

            // To make math possible.
            if (lp == 0)
            {
                lp = 1;
            }

            BigInteger newLiquidity = 0;
            if (pool.Amount0 == oldAmount0)
            {
                newLiquidity = Sqrt(newAmount0 * newAmount1);
            }
            else
            {
                newLiquidity = newAmount0 * lp / division;
            }

            liquidity = amount0 * pool.TotalLiquidity / pool.Amount0;

            Runtime.Expect(nftRAM.Liquidity - liquidity >= 0, "Trying to remove more than you have...");

            var newPool = pool;
            // If the new amount will be = 0 then burn the NFT
            if (newAmount0 == 0)
            {
                RemoveFromLPTokens(from, nftID, newPool.Symbol0, newPool.Symbol1);
                Runtime.TransferTokens(newPool.Symbol0, Address, from, oldAmount0);
                Runtime.TransferTokens(newPool.Symbol1, Address, from, oldAmount1);
            }
            
            //var holders = GetHolderList(pool.Symbol0, pool.Symbol1); 
            //holders.Remove()

            // Update the pool values
            if (pool.Amount0 == oldAmount0)
            {
                pool.Amount0 = newAmount0;
                pool.Amount1 = newAmount1;
                pool.TotalLiquidity = newLiquidity;
            }
            else
            {
                pool.Amount0 = pool.Amount0 - oldAmount0 + newAmount0;
                pool.Amount1 = pool.Amount1 - oldAmount1 + newAmount1;
                pool.TotalLiquidity = pool.TotalLiquidity - oldLP + newLiquidity;
            }

            if (pool.Amount0 == 0)
            {
                _pools.Remove($"{pool.Symbol0}_{pool.Symbol1}");
            }
            else
            {
                _pools.Set($"{pool.Symbol0}_{pool.Symbol1}", pool);
            }
        }

        public void BurnNFT(Address from, BigInteger nftID)
        {
            if ( Runtime.ProtocolVersion >= 14) Runtime.Expect(_DEXversion >= 1, " This method is not available in this version of the DEX");

            if (!Runtime.NFTExists(DomainSettings.LiquidityTokenSymbol, nftID))
                return;

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);

            LPTokenContentROM nftROM = VMObject.FromBytes(nft.ROM).AsStruct<LPTokenContentROM>();
            LPTokenContentRAM nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();

            BigInteger amount0 = nftRAM.Amount0;
            BigInteger amount1 = 0;
            string symbol0 = nftROM.Symbol0;
            string symbol1 = nftROM.Symbol1;

            Runtime.Expect(nft.CurrentOwner == from, "Invalid owner");
            if (nftRAM.Liquidity != 0)
            {
                // Claim all the fees
                ClaimFees(from, nftROM.Symbol0, nftROM.Symbol1);

                DeleteNFT(from, symbol0, amount0, symbol1, amount1, nftRAM, nftID);
            }
        }
        #endregion

        #region Utils
        /// <summary>
        /// Square root with BigInteger's
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public static BigInteger Sqrt(BigInteger n)
        {
            BigInteger root = n / 2;

            while (n < root * root)
            {
                root += n / root;
                root /= 2;
            }

            return root;
        }


        #endregion

        // Helpers
        // Runtime.GetBalance(symbol0, this.Address);
        // Runtime.GetBalance(symbol1, this.Address);
        // Runtime.GetBalance(symbol, this.Address);
        #endregion

        #endregion
        
    }
}
