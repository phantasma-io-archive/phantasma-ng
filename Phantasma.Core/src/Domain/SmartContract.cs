using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain
{
    public abstract class SmartContract : IContract
    {
        public const int SecondsInDay = 86400;

        public const string ConstructorName = "Initialize";

        public ContractInterface ABI { get; protected set; }
        public abstract string Name { get; }

        private readonly Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer()); // TODO remove this?

        public IRuntime Runtime { get; protected set; }

        private Address _address;
        public Address Address
        {
            get
            {
                if (_address.IsNull)
                {
                   _address = GetAddressFromContractName(Name);
                }

                return _address;
            }
        }

        public SmartContract()
        {
            _address = Address.Null;
        }

        public static Address GetAddressForNative(NativeContractKind kind)
        {
            return GetAddressFromContractName(kind.GetContractName());
        }

        private static Dictionary<string, Address> _contractNameMap = new Dictionary<string, Address>();

        public static Address GetAddressFromContractName(string name)
        {
            if (_contractNameMap.ContainsKey(name))
            {
                return _contractNameMap[name];
            }

            var address = Address.FromHash(name);
            _contractNameMap[name] = address;

            return address;
        }

        public static byte[] GetKeyForField(NativeContractKind nativeContract, string fieldName, bool isProtected)
        {
            return GetKeyForField(nativeContract.GetContractName(), fieldName, isProtected);
        }

        public static byte[] GetKeyForField(string contractName, string fieldName, bool isProtected)
        {
            Throw.If(string.IsNullOrEmpty(contractName), "invalid contract name");
            Throw.If(string.IsNullOrEmpty(fieldName), "invalid field name");

            string prefix = isProtected ? "." : "";

            return Encoding.UTF8.GetBytes($"{prefix}{contractName}.{fieldName}");
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
