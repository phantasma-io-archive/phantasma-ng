using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phantasma.Business.Blockchain
{
    public static class Filter
    {
        public static readonly string FilterTag = "$!";
        private static readonly string FilterRedStorage = "filter.red";
        private static readonly string FilterGreenStorage = "filter.green";

        public static bool Enabled = true;
        public static decimal Quota = 10000;

        public static readonly object Lock = new object();

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
            AddFilteredAddress(context, address, FilterRedStorage);
        }

        public static bool RemoveRedFilteredAddress(StorageContext context, Address address, string tag)
        {
            return RemoveFilteredAddress(context, address, FilterRedStorage);
        }

    }
}
