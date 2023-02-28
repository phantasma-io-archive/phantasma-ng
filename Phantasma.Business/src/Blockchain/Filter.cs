using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using System;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;

namespace Phantasma.Business.Blockchain
{
    public static class Filter
    {
        public static readonly string FilterTag = "$!";
        private static readonly string FilterRedStorage = "filter.red";
        private static readonly string FilterGreenStorage = "filter.green";
        private static readonly string FilterQuota = "filter.quota.";

        public static bool Enabled = true;
        public static decimal Quota = 50000; // Default quota is 50k
        public static decimal Threshold = 10000; // Default threshold is 10k

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
            if (runtime.ProtocolVersion >= 10)
            {
                runtime.Expect(condition, msg);
                return;
            }
            
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
            if (list.Contains(address))
                return list.Remove<Address>(address);
            return false;
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

        public static void ExpectWarning(this IRuntime Runtime, bool condition, string msg, Address address)
        {
            Runtime.CheckWarning(condition, msg, address);
            Runtime.ExpectFiltered(condition, $"{msg} expected Warning", address);
        }
        
        // This is just for warning not to stop the execution
        public static void CheckWarning(this IRuntime Runtime, bool condition, string msg, Address address ){

            if (!condition) {
                Webhook.Notify($"[{((DateTime) Runtime.Time).ToLongDateString()}] reason -> {msg} by [{address.Text}]");
            }
        }

        private static decimal GetTotalForCurrentPeriod(IRuntime Runtime, Address from)
        {
            var tag = FilterQuota + from.Text;
            var list = GetFilterStorageList(Runtime.RootStorage, tag);

            var curTime = (DateTime)Runtime.Time;

            uint idx = 0;
            var count = list.Count();

            decimal total = 0;

            while (idx < count)
            {
                var entry = list.Get<string>(idx);

                var tmp = entry.Split('|');
                var timestamp = new Timestamp(uint.Parse(tmp[0]));
                var amount = decimal.Parse(tmp[1]);

                var diff = curTime - (DateTime)timestamp;

                if (diff.TotalHours > 24)
                {
                    list.RemoveAt(idx);
                    count--;
                }
                else
                {
                    total += amount;
                    idx++;
                }
            }

            return total;
        }

        private static void AddToQuota(IRuntime Runtime, Address from, decimal worth)
        {
            var str = $"{Runtime.Time.Value}|{worth}";
            var tag = FilterQuota + from.Text;
            var list = GetFilterStorageList(Runtime.RootStorage, tag);
            list.Add<string>(str);
        }


        public static void CheckFilterAmountThreshold(this IRuntime Runtime, IToken token, Address from, BigInteger amount, string msg)
        {
            var price = UnitConversion.ToDecimal(Runtime.GetTokenPrice(token.Symbol), DomainSettings.FiatTokenDecimals);
            var worth = UnitConversion.ToDecimal(amount, token.Decimals);
            worth *= price;
            var total = GetTotalForCurrentPeriod(Runtime, from) + worth;
            
            if (Runtime.ProtocolVersion <= 9)
            {
                Runtime.CheckWarning(worth <= Filter.Threshold, $"{msg} over threshold: {worth} {token.Symbol}", from);

                Runtime.ExpectFiltered(total <= Filter.Quota, $"{msg} quota exceeded, tried to move {total} {token.Symbol} over last 24h", from);
            }
            else if (Runtime.ProtocolVersion >= 14)
            {
                Runtime.CheckWarning(worth <= Filter.Threshold, $"{msg} over quota: {worth} {token.Symbol}", from);
            } 
            else
            {
                Runtime.CheckWarning(worth <= Filter.Threshold, $"{msg} over quota: {worth} {token.Symbol}", from);
                Runtime.CheckWarning(total <= Filter.Quota, $"{msg} quota exceeded, tried to move {total} {token.Symbol} over last 24h", from);
            }
            
            AddToQuota(Runtime, from, worth);
        }

    }
}
