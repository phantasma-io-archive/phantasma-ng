using System;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class SwapContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Swap;

        public SwapContract() : base()
        {
        }

        /// <summary>
        /// Check if the token is supported
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

        public const string SwapMakerFeePercentTag = "swap.fee.maker";
        public const string SwapTakerFeePercentTag = "swap.fee.taker";

        public static readonly BigInteger SwapMakerFeePercentDefault = 2;
        public static readonly BigInteger SwapTakerFeePercentDefault = 5;

        public BigInteger GetRate(string fromSymbol, string toSymbol, BigInteger amount)
        {
            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            var exchangeVersion = Runtime.InvokeContractAtTimestamp(NativeContractKind.Exchange, nameof(ExchangeContract.GetDexVersion)).AsNumber();
            if (existsLP && exchangeVersion >= 1)
            {
                return Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.GetRate), fromSymbol,
                    toSymbol, amount).AsNumber();
            }
            else
            {
                return GetRateV2(fromSymbol, toSymbol, amount);
            }
        }

        /// <summary>
        /// Old Version to get rate.
        /// </summary>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private BigInteger GetRateV2(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(IsSupportedToken(fromSymbol), "unsupported from symbol");
            Runtime.Expect(IsSupportedToken(toSymbol), "unsupported to symbol");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(toInfo.IsFungible(), "must be fungible");

            var rate = Runtime.GetTokenQuote(fromSymbol, toSymbol, amount);

            Runtime.Expect(rate >= 0, "invalid swap rate");

            return rate;
        }

        /// <summary>
        /// Method used to deposit tokens
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
        /// Swap Fee -> Method used to convert a Symbol into KCAL, Using Pools
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="feeAmount"></param>
        public void SwapFee(Address from, string fromSymbol, BigInteger feeAmount)
        {
            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            var exchangeVersion = Runtime.InvokeContractAtTimestamp(NativeContractKind.Exchange, nameof(ExchangeContract.GetDexVersion)).AsNumber();
            if (existsLP && exchangeVersion >= 1)
            {
                Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.SwapFee), from,
                    fromSymbol, feeAmount);
            }
            else
            {
                SwapFeeV2(from, fromSymbol, feeAmount);
            }
        }

        /// <summary>
        /// Swap fee old
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="feeAmount"></param>
        private void SwapFeeV2(Address from, string fromSymbol, BigInteger feeAmount)
        {
            var feeSymbol = DomainSettings.FuelTokenSymbol;
            var feeBalance = Runtime.GetBalance(feeSymbol, from);
            feeAmount -= feeBalance;
            if (feeAmount <= 0)
            {
                return;
            }

            var amountInOtherSymbol = GetRate(feeSymbol, fromSymbol, feeAmount);

            var token = Runtime.GetToken(fromSymbol);
            BigInteger minAmount;

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
                    minAmount = 1;
                }
            }

            if (amountInOtherSymbol < minAmount)
            {
                amountInOtherSymbol = minAmount;
            }

            // round up
            amountInOtherSymbol++;

            SwapTokens(from, fromSymbol, feeSymbol, amountInOtherSymbol);

            var finalFeeBalance = Runtime.GetBalance(feeSymbol, from);
            Runtime.Expect(finalFeeBalance >= feeAmount, $"something went wrong in swapfee finalFeeBalance: {finalFeeBalance} feeAmount: {feeAmount}");
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
            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            var exchangeVersion = Runtime.InvokeContractAtTimestamp(NativeContractKind.Exchange, nameof(ExchangeContract.GetDexVersion)).AsNumber();
            if (existsLP && exchangeVersion >= 1)
            {
                Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.SwapReverse), from,
                    fromSymbol, toSymbol, total);
            }
            else
            {
                var amount = GetRate(toSymbol, fromSymbol, total);
                Runtime.Expect(amount > 0, $"cannot reverse swap {fromSymbol}");
                SwapTokens(from, fromSymbol, toSymbol, amount);
            }

        }

        /// <summary>
        /// To swap tokens
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        public void SwapTokens(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            var exchangeVersion = Runtime.InvokeContractAtTimestamp(NativeContractKind.Exchange, nameof(ExchangeContract.GetDexVersion)).AsNumber();
            if (existsLP && exchangeVersion >= 1)
            {
                Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.SwapTokens), from,
                    fromSymbol, toSymbol, amount);
            }
            else
            {
                SwapTokensV2(from, fromSymbol, toSymbol, amount);
            }
        }

        /// <summary>
        /// Swap token OldVersion
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        public void SwapTokensV2(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount > 0, "invalid amount");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(IsSupportedToken(fromSymbol), "source token is unsupported");

            var fromBalance = Runtime.GetBalance(fromSymbol, from);
            Runtime.Expect(fromBalance > 0, $"not enough {fromSymbol} balance");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(IsSupportedToken(toSymbol), "destination token is unsupported");

            var total = GetRate(fromSymbol, toSymbol, amount);

            Runtime.Expect(total > 0, "amount to swap needs to be larger than zero");

            var toPotBalance = Runtime.GetBalance(toSymbol, Address);

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

            var half = toPotBalance / 2;
            Runtime.Expect(total < half, $"taking too much {toSymbol} from pot at once");

            Runtime.TransferTokens(fromSymbol, from, Address, amount);
            Runtime.TransferTokens(toSymbol, Address, from, total);
        }
    }
}
