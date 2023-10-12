using System;
using System.Collections.Generic;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;
using Phantasma.Infrastructure.Pay.Enums;
using Phantasma.Infrastructure.Pay.Structs;

namespace Phantasma.Infrastructure.Pay.Chains
{
    public class PhantasmaWallet : CryptoWallet
    {
        private string rpcURL;

        public PhantasmaWallet(PhantasmaKeys keys, string rpcURL) : base(keys)
        {
            if (!rpcURL.EndsWith("/"))
            {
                rpcURL += "/";
            }
            this.rpcURL = rpcURL;
        }

        public const string PhantasmaPlatform = "phantasma";

        public override string Platform => PhantasmaPlatform;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public override void SyncBalances(Action<bool> callback)
        {
            _balances.Clear();

            var url = $"{rpcURL}getAccount/{Address}"; 
            JSONRequest(url, (response) =>
            {
                if (response == null)
                {
                    callback(false);
                    return;
                }

                this.Name = response.RootElement.GetProperty("name").GetString();

                if (response.RootElement.TryGetProperty("balances", out var balanceNode))
                {
                    foreach (var child in balanceNode.EnumerateArray())
                    {
                        var symbol = child.GetProperty("symbol").GetString();
                        var decimals = child.GetProperty("decimals").GetInt32();
                        var chain = child.GetProperty("chain").GetString();

                        var temp = child.GetProperty("amount").GetString();
                        var n = BigInteger.Parse(temp);
                        var amount = UnitConversion.ToDecimal(n, decimals);

                        _balances.Add(new WalletBalance(symbol, amount, chain));
                    }
                }

                callback(true);
            });
            callback(false);
        }

        protected override string DeriveAddress(PhantasmaKeys keys)
        {
            return keys.Address.Text; 
        }

        public override IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos()
        {
            yield return new CryptoCurrencyInfo("SOUL", "Phantasma Stake", 8, PhantasmaWallet.PhantasmaPlatform, CryptoCurrencyCaps.Balance | CryptoCurrencyCaps.Transfer | CryptoCurrencyCaps.Stake);
            yield return new CryptoCurrencyInfo("KCAL", "Phantasma Energy", 10, PhantasmaWallet.PhantasmaPlatform, CryptoCurrencyCaps.Balance | CryptoCurrencyCaps.Transfer);
            yield break;
        }
    }
}
