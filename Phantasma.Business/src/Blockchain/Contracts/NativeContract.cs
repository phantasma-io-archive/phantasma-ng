using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using Phantasma.Core;
using Phantasma.Core.Domain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Structs;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;
using Phantasma.Core.Storage.Context.Interfaces;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Business.Blockchain.Contracts
{
    public static class ContractPatch
    {
        public static readonly uint UnstakePatch = 1578238531;
    }

    public abstract class NativeContract : SmartContract, INativeContract
    {
        public override string Name => Kind.GetContractName();

        public abstract NativeContractKind Kind { get; }

        private Dictionary<string, MethodInfo> _methodTable = new Dictionary<string, MethodInfo>();

        public NativeContract() : base()
        {
            BuildMethodTable();
        }

        /// <summary>
        /// Set Runtime 
        /// </summary>
        /// <param name="runtime"></param>
        public void SetRuntime(IRuntime runtime)
        {
            if (Runtime != null && Runtime != runtime)
            {
                runtime.Throw("runtime already set on this contract");
            }

            Runtime = runtime;
        }

        /// <summary>
        /// Load all the field from the storage
        /// here we auto-initialize any fields from storage
        /// </summary>
        /// <param name="storage"></param>
        public void LoadFromStorage(StorageContext storage)
        {
            var contractType = GetType();
            FieldInfo[] fields = contractType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var baseKey = GetKeyForField(Name, field.Name, true);

                var isStorageField = typeof(IStorageCollection).IsAssignableFrom(field.FieldType);
                if (isStorageField)
                {
                    var args = new object[] { baseKey, storage };
                    var obj = Activator.CreateInstance(field.FieldType, args);

                    field.SetValue(this, obj);
                    continue;
                }

                if (typeof(ISerializable).IsAssignableFrom(field.FieldType))
                {
                    ISerializable obj;

                    if (storage.Has(baseKey))
                    {
                        var bytes = storage.Get(baseKey);
                        obj = (ISerializable)Activator.CreateInstance(field.FieldType);
                        using (var stream = new MemoryStream(bytes))
                        {
                            using (var reader = new BinaryReader(stream))
                            {
                                obj.UnserializeData(reader);
                            }
                        }

                        field.SetValue(this, obj);
                        continue;
                    }
                }

                if (storage.Has(baseKey))
                {
                    var obj = storage.Get(baseKey, field.FieldType);
                    field.SetValue(this, obj);
                    continue;
                }
            }
        }

        /// <summary>
        /// Load a field from storage
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="kind"></param>
        /// <param name="fieldName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T LoadFieldFromStorage<T>(StorageContext storage, NativeContractKind kind, string fieldName)
        {
            var contractName = kind.GetContractName();
            var key = GetKeyForField(contractName, fieldName, true);
            if (storage.Has(key))
            {
                return storage.Get<T>(key);
            }

            return default;
        }

        /// <summary>
        /// Save changes to storage
        /// here we persist any modifed fields back to storage
        /// </summary>
        public void SaveChangesToStorage()
        {
            Throw.IfNull(Runtime, nameof(Runtime));

            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            var contractType = GetType();
            FieldInfo[] fields = contractType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var baseKey = GetKeyForField(Name, field.Name, true);

                var isStorageField = typeof(IStorageCollection).IsAssignableFrom(field.FieldType);
                if (isStorageField)
                {
                    continue;
                }

                if (typeof(ISerializable).IsAssignableFrom(field.FieldType))
                {
                    var obj = (ISerializable)field.GetValue(this);
                    var bytes = obj.Serialize();
                    Runtime.Storage.Put(baseKey, bytes);
                }
                else
                {
                    var obj = field.GetValue(this);
                    var bytes = obj.Serialize();
                    Runtime.Storage.Put(baseKey, bytes);
                }
            }
        }

        #region METHOD TABLE

        /// <summary>
        /// Build the method table
        /// </summary>
        private void BuildMethodTable()
        {
            var type = GetType();

            var srcMethods = type.GetMethods(BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);
            var methods = new List<ContractMethod>();

            foreach (var srcMethod in srcMethods)
            {
                var parameters = new List<ContractParameter>();
                var srcParams = srcMethod.GetParameters();

                var methodName = srcMethod.Name;

                if (methodName.StartsWith("get_"))
                {
                    methodName = methodName.Substring(4);
                }

                if (methodName == "Kind")
                {
                    continue;
                }

                var isVoid = srcMethod.ReturnType == typeof(void);
                var returnType = isVoid ? VMType.None : VMObject.GetVMType(srcMethod.ReturnType);

                bool isValid = isVoid || returnType != VMType.None;
                if (!isValid)
                {
                    continue;
                }

                foreach (var srcParam in srcParams)
                {
                    var paramType = srcParam.ParameterType;
                    var vmtype = VMObject.GetVMType(paramType);

                    if (vmtype != VMType.None)
                    {
                        parameters.Add(new ContractParameter(srcParam.Name, vmtype));
                    }
                    else
                    {
                        isValid = false;
                        break;
                    }
                }

                if (isValid)
                {
                    _methodTable[methodName] = srcMethod;
                    var method = new ContractMethod(methodName, returnType, -1, parameters.ToArray());
                    methods.Add(method);
                }
            }

            ABI = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
        }

        /// <summary>
        /// Check if a method exists on this contract
        /// </summary>
        /// <param name="methodName"></param>
        /// <returns></returns>
        public bool HasInternalMethod(string methodName)
        {
            return _methodTable.ContainsKey(methodName);
        }

        /// <summary>
        /// Call an internal method on this contract
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="name"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public object CallInternalMethod(IRuntime runtime, string name, object[] args)
        {
            Throw.If(!_methodTable.ContainsKey(name), "unknowm internal method");

            var method = _methodTable[name];
            Throw.IfNull(method, nameof(method));

            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = CastArgument(runtime, args[i], parameters[i].ParameterType);
            }

            return method.Invoke(this, args);
        }

        /// <summary>
        /// Cast an argument to the expected type
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="arg"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private object CastArgument(IRuntime runtime, object arg, Type expectedType)
        {
            if (arg == null)
            {
                if (expectedType.IsArray)
                {
                    var elementType = expectedType.GetElementType();
                    var result = Array.CreateInstance(elementType, 0);
                    return result;
                }

                throw new Exception("Invalid cast for null VM object");
            }

            var receivedType = arg.GetType();
            if (expectedType == receivedType)
            {
                return arg;
            }

            if (expectedType.IsArray)
            {
                if (expectedType == typeof(byte[]))
                {
                    if (receivedType == typeof(string))
                    {
                        return Encoding.UTF8.GetBytes((string)arg);
                    }

                    if (receivedType == typeof(BigInteger))
                    {
                        return ((BigInteger)arg).ToByteArray();
                    }

                    if (receivedType == typeof(Hash))
                    {
                        return ((Hash)arg).ToByteArray();
                    }

                    if (receivedType == typeof(Address))
                    {
                        return ((Address)arg).ToByteArray();
                    }

                    throw new Exception("cannot cast this object to a byte array");
                }
                else
                {
                    if (Runtime.ProtocolVersion >= 11)
                    {
                        var elementType = expectedType.GetElementType();

                        if (arg == typeof(Dictionary<VMObject, VMObject>))
                        {
                            var dic = (Dictionary<VMObject, VMObject>)arg;
                            var array = Array.CreateInstance(elementType, dic.Count);
                            for (int i = 0; i < array.Length; i++)
                            {
                                var key = new VMObject();
                                key.SetValue(i);

                                var val = dic[key].Data;
                                val = CastArgument(runtime, val, elementType);
                                array.SetValue(val, i);
                            }

                            return array;
                        }
                        else if (arg == typeof(VMObject))
                        {
                            var vmObj = (VMObject)arg;
                            var array = vmObj.ToArray(elementType);
                            return array;
                        }
                        else if (Runtime.ProtocolVersion >= 12)
                        {
                            var dic = (Dictionary<VMObject, VMObject>)arg;
                            var array = Array.CreateInstance(elementType, dic.Count);
                            for (int i = 0; i < array.Length; i++)
                            {
                                var key = new VMObject();
                                key.SetValue(i);

                                var val = dic[key].Data;
                                val = CastArgument(runtime, val, elementType);
                                array.SetValue(val, i);
                            }

                            return array;
                        }
                    }
                    else
                    {
                        var dic = (Dictionary<VMObject, VMObject>)arg;
                        var elementType = expectedType.GetElementType();
                        var array = Array.CreateInstance(elementType, dic.Count);
                        for (int i = 0; i < array.Length; i++)
                        {
                            var key = new VMObject();
                            key.SetValue(i);

                            var val = dic[key].Data;
                            val = CastArgument(runtime, val, elementType);
                            array.SetValue(val, i);
                        }

                        return array;
                    }
                }
            }

            if (expectedType.IsEnum)
            {
                if (!receivedType.IsEnum)
                {
                    arg = Enum.Parse(expectedType, arg.ToString());
                    return arg;
                }
            }

            if (expectedType == typeof(Address))
            {
                if (receivedType == typeof(string))
                {
                    var text = (string)arg;
                    Address address;

                    if (Address.IsValidAddress(text))
                    {
                        address = Address.FromText(text);
                    }
                    else
                    {
                        // when a name string is passed instead of an address we do an automatic lookup and replace
                        address = runtime.LookUpName(text);
                    }

                    return address;
                }
            }

            if (expectedType == typeof(BigInteger))
            {
                if (receivedType == typeof(string))
                {
                    var value = (string)arg;
                    if (BigInteger.TryParse(value, out BigInteger number))
                    {
                        arg = number;
                    }
                }
            }

            if (Runtime.ProtocolVersion > 8)
            {
                if (expectedType == typeof(Timestamp))
                {
                    if (receivedType == typeof(string))
                    {
                        var value = (string)arg;
                        if (uint.TryParse(value, out uint timestamp))
                        {
                            arg = new Timestamp(timestamp);
                        }
                    }
                    else if (receivedType == typeof(BigInteger))
                    {
                        var value = (BigInteger)arg;
                        arg = new Timestamp((uint)value);
                    }
                    else if (receivedType == typeof(Timestamp))
                    {
                        return arg;
                    }
                    else if (receivedType == typeof(DateTime))
                    {
                        var value = (DateTime)arg;
                        arg = (Timestamp)value;
                    }
                    else if (receivedType == typeof(uint))
                    {
                        var value = (uint)arg;
                        arg = (Timestamp)value;
                    }
                }
            }

            if (typeof(ISerializable).IsAssignableFrom(expectedType))
            {
                if (receivedType == typeof(byte[]))
                {
                    var bytes = (byte[])arg;
                    arg = Serialization.Unserialize(bytes, expectedType);
                    return arg;
                }
            }

            return arg;
        }

        #endregion

        private static Dictionary<Address, Type> _nativeContractMap = null;

        /// <summary>
        /// Register a Contract
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private static void RegisterContract<T>() where T : NativeContract
        {
            var alloc = (NativeContract)Activator.CreateInstance<T>();
            var addr = alloc.Address;
            _nativeContractMap[addr] = typeof(T);
        }

        /// <summary>
        /// Returns the Native Contract by its name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static NativeContract GetNativeContractByName(string name)
        {
            var kind = name.FindNativeContractKindByName();
            return GetNativeContractByKind(kind);
        }

        /// <summary>
        /// Returns the Native Contract by its kind
        /// </summary>
        /// <param name="kind"></param>
        /// <returns></returns>
        public static NativeContract GetNativeContractByKind(NativeContractKind kind)
        {
            if (kind == NativeContractKind.Unknown)
            {
                return null;
            }

            var address = GetAddressForNative(kind);
            return GetNativeContractByAddress(address);
        }

        /// <summary>
        /// Returns the Native Contract by its address 
        /// </summary>
        /// <param name="contractAddress"></param>
        /// <returns></returns>
        public static NativeContract GetNativeContractByAddress(Address contractAddress)
        {
            if (_nativeContractMap == null)
            {
                _nativeContractMap = new Dictionary<Address, Type>();
                RegisterContract<ValidatorContract>();
                RegisterContract<GovernanceContract>();
                RegisterContract<ConsensusContract>();
                RegisterContract<AccountContract>();
                RegisterContract<FriendsContract>();
                RegisterContract<ExchangeContract>();
                RegisterContract<MarketContract>();
                RegisterContract<StakeContract>();
                RegisterContract<SwapContract>();
                RegisterContract<GasContract>();
                RegisterContract<PrivacyContract>();
                RegisterContract<BlockContract>();
                RegisterContract<RelayContract>();
                RegisterContract<StorageContract>();
                RegisterContract<InteropContract>();
                RegisterContract<RankingContract>();
                RegisterContract<FriendsContract>();
                RegisterContract<MailContract>();
                RegisterContract<SaleContract>();
            }

            if (_nativeContractMap.ContainsKey(contractAddress))
            {
                var type = _nativeContractMap[contractAddress];
                return (NativeContract)Activator.CreateInstance(type);
            }

            return null;
        }
    }
}
