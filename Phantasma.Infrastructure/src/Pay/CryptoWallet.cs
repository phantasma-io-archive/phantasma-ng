using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using Phantasma.Core.Cryptography;

namespace Phantasma.Infrastructure.Pay
{
    public abstract class CryptoWallet
    {
        public abstract string Platform { get; }
        public readonly string Address;
        public string Name { get; protected set; }

        protected List<WalletBalance> _balances = new List<WalletBalance>();
        public IEnumerable<WalletBalance> Balances => _balances;

        public CryptoWallet(PhantasmaKeys keys)
        {
            this.Address = DeriveAddress(keys);
            this.Name = Address;
        }

        protected void FetchURL(string url, Action<string> callback)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = httpClient.GetAsync(url).Result;
                    using (var content = response.Content)
                    {
                        callback(content.ReadAsStringAsync().Result);
                    }
                }
            }
            catch
            {
                callback(null);
            }
        }

        protected abstract string DeriveAddress(PhantasmaKeys keys);

        public abstract void SyncBalances(Action<bool> callback);
        public abstract void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback);

        public abstract IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos();

        protected void JSONRequest(string url, Action<JsonDocument> callback)
        {
            FetchURL(url, (json) =>
            {
                if (string.IsNullOrEmpty(json))
                {
                    callback(null);
                }
                else
                {
                    try
                    {
                        var root = JsonDocument.Parse(json);
                        callback(root);
                    }
                    catch
                    {
                        callback(null);
                    }
                }
            });
        }
    }
}
