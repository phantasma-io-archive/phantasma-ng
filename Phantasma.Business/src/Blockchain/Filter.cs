using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using System;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Numerics;

namespace Phantasma.Business.Blockchain
{
    public static class Filter
    {
        public static readonly string FilterTag = "$!";
        private static readonly string FilterRedStorage = "filter.red";
        private static readonly string FilterGreenStorage = "filter.green";

        public static bool Enabled = true;
        public static decimal Quota = 20000;
        public static decimal Threshold = 5000;

        private static readonly object Lock = new object();

        public static void Test(Action callback)
        {
            lock (Lock)
            {
                Enabled = false;
                callback();
                Enabled = true;
            }
        }

        public static void ExpectFiltered(this IRuntime runtime, bool condition, string msg, Address address)
        {
            if (!Enabled)
            {
                return;
            }

            if (IsFilteredAddress(runtime.RootStorage, address, FilterGreenStorage))
            {
                return;
            }

            if (!condition)
            {
                condition = false; // useless statement, here just for breakpoints
            }

            runtime.Expect(condition, $"{address}{FilterTag}{msg}");
        }

        public static Address ExtractFilteredAddress(string exceptionMessage)
        {
            var idx = exceptionMessage.IndexOf(FilterTag);
            if (idx < 0)
            {
                return Address.Null;
            }

            var str = exceptionMessage.Substring(0, idx);

            return Address.FromText(str);
        }

        private static StorageList GetFilterStorageList(StorageContext context, string tag)
        {
            return new StorageList(tag, context);
        }

        private static bool IsFilteredAddress(StorageContext context, Address address, string tag)
        {
            var list = GetFilterStorageList(context, tag);
            return list.Contains<Address>(address);
        }

        private static void AddFilteredAddress(StorageContext context, Address address, string tag)
        {
            var list = GetFilterStorageList(context, tag);
            if (list.Contains<Address>(address))
            {
                return;
            }

            list.Add<Address>(address);
        }

        private static bool RemoveFilteredAddress(StorageContext context, Address address, string tag)
        {
            var list = GetFilterStorageList(context, tag);
            return list.Remove<Address>(address);
        }

        public static bool IsGreenFilteredAddress(StorageContext context, Address address)
        {
            return IsFilteredAddress(context, address, FilterGreenStorage);
        }

        public static void AddGreenFilteredAddress(StorageContext context, Address address)
        {
            if ( IsRedFilteredAddress(context, address) )
                RemoveRedFilteredAddress(context, address, FilterRedStorage);
            AddFilteredAddress(context, address, FilterGreenStorage);
        }

        public static bool RemoveGreenFilteredAddress(StorageContext context, Address address, string tag)
        {
            return RemoveFilteredAddress(context, address, FilterGreenStorage);
        }

        public static bool IsRedFilteredAddress(StorageContext context, Address address)
        {
            return IsFilteredAddress(context, address, FilterRedStorage);
        }

        public static void AddRedFilteredAddress(StorageContext context, Address address)
        {
            if ( IsGreenFilteredAddress(context, address) )
                RemoveGreenFilteredAddress(context, address, FilterGreenStorage);
            Webhook.Notify($"[{DateTime.UtcNow.ToLongDateString()}] Address added to filtered [{address.Text}]");
            AddFilteredAddress(context, address, FilterRedStorage);
        }

        public static bool RemoveRedFilteredAddress(StorageContext context, Address address, string tag)
        {
            Webhook.Notify($"[{DateTime.UtcNow.ToLongDateString()}] Address removed from filtered [{address.Text}]");
            return RemoveFilteredAddress(context, address, FilterRedStorage);
        }
        
        // This is just for warning not to stop the execution
        public static void Warning(this IRuntime Runtime, bool condition, string msg, Address address ){

            if (!condition) {
                Webhook.Notify($"[{((DateTime) Runtime.Time).ToLongDateString()}] reason -> {msg} by [{address.Text}]");
            }
        }
        
        public static void CheckFilterAmountThreshold(this IRuntime runtime, IToken token, Address from, BigInteger amount, string msg)
        {
            var price = UnitConversion.ToDecimal(runtime.GetTokenPrice(token.Symbol), DomainSettings.FiatTokenDecimals);
            var total = UnitConversion.ToDecimal(amount, token.Decimals);
            var worth = price * total;
            runtime.ExpectFiltered(worth <= Filter.Quota, $"{msg} quota exceeded, tried to move {total} {token.Symbol}", from);
            runtime.Warning(worth <= Filter.Threshold, $"{msg} threshold reached {total} {token.Symbol}", from);
        }

    }
}
