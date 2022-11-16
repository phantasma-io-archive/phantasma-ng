using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;

namespace Phantasma.Business.Blockchain.Contracts
{
    public sealed class SwapContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Swap;
        
        public SwapContract() : base()
        {
        }

        public const string SwapMakerFeePercentTag = "swap.fee.maker";
        public const string SwapTakerFeePercentTag = "swap.fee.taker";
        
        public static readonly BigInteger SwapMakerFeePercentDefault = 2;
        public static readonly BigInteger SwapTakerFeePercentDefault = 5;
        
        /// <summary>
        /// Swap Fee -> Method used to convert a Symbol into KCAL, Using Pools
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="feeAmount"></param>
        public void SwapFee(Address from, string fromSymbol, BigInteger feeAmount)
        {
            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            if (existsLP)
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

            var amountInOtherSymbol = Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.GetRate), feeSymbol, fromSymbol, feeAmount).AsNumber();

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
            Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.SwapReverse), from,
                fromSymbol, toSymbol, total);
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
            Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.SwapTokens), from,
                fromSymbol, toSymbol, amount);
        }
    }
}
