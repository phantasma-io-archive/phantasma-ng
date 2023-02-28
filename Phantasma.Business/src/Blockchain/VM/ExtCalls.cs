using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Core;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;

namespace Phantasma.Business.Blockchain.VM
{
    public delegate ExecutionState ExtcallDelegate(RuntimeVM vm);

    public static class ExtCalls
    {
        // naming scheme should be "namespace.methodName" for methods, and "type()" for constructors
        internal static void RegisterWithRuntime(RuntimeVM vm)
        {
            IterateExtcalls((name, argCount, method) =>
            {
                vm.RegisterMethod(name, argCount, method);
            });
        }

        internal static void IterateExtcalls(Action<string, int, ExtcallDelegate> callback)
        {
            callback("Runtime.TransactionHash", 0, Runtime_TransactionHash); // --> done
            callback("Runtime.Time", 0, Runtime_Time);
            callback("Runtime.Version", 0, Runtime_Version);
            callback("Runtime.GasTarget", 0, Runtime_GasTarget);
            callback("Runtime.Validator", 0, Runtime_Validator);
            callback("Runtime.Context", 0, Runtime_Context);
            callback("Runtime.PreviousContext", 0, Runtime_PreviousContext);
            callback("Runtime.GenerateUID", 0, Runtime_GenerateUID);
            callback("Runtime.IsWitness", 1, Runtime_IsWitness);
            callback("Runtime.IsTrigger", 0, Runtime_IsTrigger);
            callback("Runtime.IsMinter", 2, Runtime_IsMinter);
            callback("Runtime.Log", 1, Runtime_Log);
            callback("Runtime.Notify", 3, Runtime_Notify);
            callback("Runtime.DeployContract", 4, Runtime_DeployContract);
            callback("Runtime.UpgradeContract", 3, Runtime_UpgradeContract);
            callback("Runtime.KillContract", 2, Runtime_KillContract);
            callback("Runtime.GetBalance", 2, Runtime_GetBalance);
            callback("Runtime.TransferTokens", 4, Runtime_TransferTokens);
            callback("Runtime.TransferBalance", 3, Runtime_TransferBalance);
            callback("Runtime.MintTokens", 4, Runtime_MintTokens);
            callback("Runtime.BurnTokens", 3, Runtime_BurnTokens);
            callback("Runtime.SwapTokens", 5, Runtime_SwapTokens);
            callback("Runtime.TransferToken", 4, Runtime_TransferToken);
            callback("Runtime.MintToken", 6, Runtime_MintToken);
            callback("Runtime.BurnToken", 3, Runtime_BurnToken);
            callback("Runtime.InfuseToken", 5, Runtime_InfuseToken);
            callback("Runtime.ReadTokenROM", 2, Runtime_ReadTokenROM);
            callback("Runtime.ReadTokenRAM", 2, Runtime_ReadTokenRAM);
            callback("Runtime.ReadToken", 2, Runtime_ReadToken);
            callback("Runtime.WriteToken", 4, Runtime_WriteToken);
            callback("Runtime.TokenExists", 1, Runtime_TokenExists);
            callback("Runtime.GetTokenDecimals", 1, Runtime_TokenGetDecimals);
            callback("Runtime.GetTokenFlags", 1, Runtime_TokenGetFlags);

            callback("Nexus.GetGovernanceValue", 1, Nexus_GetGovernanceValue);
            callback("Nexus.BeginInit", 1, Nexus_BeginInit);
            callback("Nexus.EndInit", 1, Nexus_EndInit);

            callback("Nexus.CreateToken", 3, Nexus_CreateToken);
            callback("Nexus.CreateTokenSeries", 7, Nexus_CreateTokenSeries);
            //callback("Nexus.CreateChain", Nexus_CreateChain); // currently unused
            //callback("Nexus.CreatePlatform", Nexus_CreatePlatform); // to remove
            callback("Nexus.CreateOrganization", 4, Nexus_CreateOrganization);
            //callback("Nexus.SetPlatformTokenHash", Nexus_SetPlatformTokenHash); // to remove

            callback("Organization.AddMember", 3, Organization_AddMember);
            callback("Organization.RemoveMember", 3, Organization_RemoveMember);

            //callback("Task.Start", Task_Start); currently unused, needs tests
            //callback("Task.Stop", Task_Stop);
            //callback("Task.Get", Task_Get);
            //callback("Task.Current", Task_Current);

            callback("Data.Get", 3, Data_Get);
            callback("Data.Set", 2, Data_Set);
            callback("Data.Delete", 1, Data_Delete);

            callback("Map.Has", 4, Map_Has);
            callback("Map.Get", 4, Map_Get);
            callback("Map.Set", 3, Map_Set);
            callback("Map.Remove", 2, Map_Remove);
            callback("Map.Count", 2, Map_Count);
            callback("Map.Clear", 1, Map_Clear);
            callback("Map.Keys", 2, Map_Keys);

            callback("List.Get", 4, List_Get);
            callback("List.Add", 2, List_Add);
            callback("List.Replace", 3, List_Replace);
            //callback("List.Remove", List_Remove); TODO implement later, remove by value instead of index
            callback("List.RemoveAt", 2, List_RemoveAt);
            callback("List.Count", 2, List_Count);
            callback("List.Clear", 1, List_Clear);

            callback("Account.Name", 1, Account_Name);
            callback("Account.LastActivity", 1, Account_Activity);
            callback("Account.Transactions", 1, Account_Transactions);

            callback("Oracle.Read", 1, Oracle_Read);
            callback("Oracle.Price", 1, Oracle_Price);
            callback("Oracle.Quote", 3, Oracle_Quote);
            // TODO
            //callback("Oracle.Block", Oracle_Block);
            //callback("Oracle.Transaction", Oracle_Transaction);
            /*callback("Oracle.Register", Oracle_Register);
            callback("Oracle.List", Oracle_List);
            */

            callback("ABI()", 1, Constructor_ABI);
            callback("Address()", 1, Constructor_AddressV2);
            callback("Hash()", 1, Constructor_Hash);
            callback("Timestamp()", 1, Constructor_Timestamp);
        }

        private static ExecutionState Constructor_Object<IN, OUT>(VirtualMachine vm, Func<IN, OUT> loader)
        {
            var rawInput = vm.Stack.Pop();
            var inputType = VMObject.GetVMType(typeof(IN));
            var convertedInput = rawInput.AsType(inputType);

            try
            {
                OUT obj = loader((IN)convertedInput);
                var temp = new VMObject();
                temp.SetValue(obj);
                vm.Stack.Push(temp);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        public static ExecutionState Constructor_Address(VirtualMachine vm)
        {
            return Constructor_Object<byte[], Address>(vm, bytes =>
            {
                if (bytes == null || bytes.Length == 0)
                {
                    return Address.Null;
                }

                var addressData = bytes;
                if (addressData.Length == Address.LengthInBytes + 1)
                {
                    addressData = addressData.Skip(1).ToArray(); // HACK this is to work around sometimes addresses being passed around in Serializable format...
                }

                Throw.If(addressData.Length != Address.LengthInBytes, "cannot build Address from invalid data");
                return Address.FromBytes(addressData);
            });
        }

        public static ExecutionState Constructor_AddressV2(RuntimeVM vm)
        {
            var addr = vm.PopAddress();
            var temp = new VMObject();
            temp.SetValue(addr);
            vm.Stack.Push(temp);
            return ExecutionState.Running;
        }

        public static ExecutionState Constructor_Hash(VirtualMachine vm)
        {
            return Constructor_Object<byte[], Hash>(vm, bytes =>
            {
                Throw.If(bytes == null || bytes.Length != Hash.Length, "cannot build Hash from invalid data");
                return new Hash(bytes);
            });
        }

        public static ExecutionState Constructor_Timestamp(VirtualMachine vm)
        {
            return Constructor_Object<BigInteger, Timestamp>(vm, val =>
            {
                Throw.If(val < 0, "invalid number");
                return new Timestamp((uint)val);
            });
        }

        public static ExecutionState Constructor_ABI(VirtualMachine vm)
        {
            return Constructor_Object<byte[], ContractInterface>(vm, bytes =>
            {
                Throw.If(bytes == null, "invalid abi");

                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        return ContractInterface.Unserialize(reader);
                    }
                }
            });
        }

        private static ExecutionState Runtime_Log(RuntimeVM vm)
        {
            var text = vm.Stack.Pop().AsString();
            vm.Log(text);
            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_Notify(RuntimeVM vm)
        {
            vm.Expect(vm.CurrentContext.Name != VirtualMachine.EntryContextName, "cannot notify in current context");

            var kind = vm.Stack.Pop().AsEnum<EventKind>();
            var address = vm.PopAddress();
            var obj = vm.Stack.Pop();

            var bytes = obj.Serialize();
            var str = Serialization.Unserialize<string>(bytes);

            vm.Notify(kind, address, bytes);
            return ExecutionState.Running;
        }

        #region ORACLES
        // TODO proper exceptions
        private static ExecutionState Oracle_Read(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var url = vm.PopString("url");

            if (vm.Oracle == null)
            {
                return ExecutionState.Fault;
            }

            url = url.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(url))
            {
                return ExecutionState.Fault;
            }

            var result = vm.Oracle.Read<byte[]>(vm.Time,/*vm.Transaction.Hash, */url);
            vm.Stack.Push(VMObject.FromObject(result));

            return ExecutionState.Running;
        }

        private static ExecutionState Oracle_Price(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var symbol = vm.PopString("price");

            var price = vm.GetTokenPrice(symbol);

            vm.Stack.Push(VMObject.FromObject(price));

            return ExecutionState.Running;
        }

        private static ExecutionState Oracle_Quote(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var amount = vm.PopNumber("amount");
            var quoteSymbol = vm.PopString("quoteSymbol");
            var baseSymbol = vm.PopString("baseSymbol");

            var price = vm.GetTokenQuote(baseSymbol, quoteSymbol, amount);

            vm.Stack.Push(VMObject.FromObject(price));

            return ExecutionState.Running;
        }

        /*
        private static ExecutionState Oracle_Register(RuntimeVM vm)
        {
            ExpectStackSize(vm, 2);

            VMObject temp;

            temp = vm.Stack.Pop();
            if (temp.Type != VMType.Object)
            {
                return ExecutionState.Fault;
            }

            var address = temp.AsInterop<Address>();

            temp = vm.Stack.Pop();
            if (temp.Type != VMType.String)
            {
                return ExecutionState.Fault;
            }

            var name = temp.AsString();

            return ExecutionState.Running;
        }

        // should return list of all registered oracles
        private static ExecutionState Oracle_List(RuntimeVM vm)
        {
            throw new NotImplementedException();
        }*/

        #endregion

        private static ExecutionState Runtime_Time(RuntimeVM vm)
        {
            var result = new VMObject();
            result.SetValue(vm.Time);
            vm.Stack.Push(result);
            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_Version(RuntimeVM vm)
        {
            var result = new VMObject();
            result.SetValue(new BigInteger(vm.ProtocolVersion));
            vm.Stack.Push(result);
            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransactionHash(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                var result = new VMObject();
                result.SetValue(tx.Hash);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_IsMinter(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                vm.ExpectStackSize(2);

                var address = vm.PopAddress();
                var symbol = vm.PopString("symbol");

                bool success = vm.IsMintingAddress(address, symbol);

                var result = new VMObject();
                result.SetValue(success);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_GasTarget(RuntimeVM vm)
        {
            if (vm.GasTarget.IsNull)
            {
                throw new VMException(vm, "Gas target is not available yet");
            }

            var result = new VMObject();
            result.SetValue(vm.GasTarget);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_Validator(RuntimeVM vm)
        {
            var result = new VMObject();
            result.SetValue(vm.Validator);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_Context(RuntimeVM vm)
        {
            var result = new VMObject();
            result.SetValue(vm.CurrentContext.Name);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_PreviousContext(RuntimeVM vm)
        {
            var result = new VMObject();

            if (vm.PreviousContext != null)
            {
                result.SetValue(vm.PreviousContext.Name);
            }
            else
            {
                result.SetValue(VirtualMachine.EntryContextName);
            }

            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_GenerateUID(RuntimeVM vm)
        {
            try
            {
                var number = vm.GenerateUID();

                var result = new VMObject();
                result.SetValue(number);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_IsWitness(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                vm.ExpectStackSize(1);

                var address = vm.PopAddress();

                var success = vm.IsWitness(address);

                var result = new VMObject();
                result.SetValue(success);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_IsTrigger(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                var success = vm.IsTrigger;

                var result = new VMObject();
                result.SetValue(success);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        #region DATA
        private static ExecutionState Data_Get(RuntimeVM vm)
        {
            // NOTE: having this check here prevents NFT properties from working
            //vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(3);

            var contractName = vm.PopString("contract");

            var field = vm.PopString("field");
            var key = SmartContract.GetKeyForField(contractName, field, false);

            vm.Expect(vm.ContractDeployed(contractName), $"contract '{contractName}' is not deployed when trying to fetch field '{field}'");

            var type_obj = vm.Stack.Pop();
            var vmType = type_obj.AsEnum<VMType>();

            if (vmType == VMType.Object)
            {
                vmType = VMType.Bytes;
            }

            var value_bytes = vm.Storage.Get(key);
            var val = new VMObject();
            val.SetValue(value_bytes, vmType);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }

        /// <summary>
        /// Set a field in the current contract
        /// Can only be called from within a contract.
        /// </summary>
        /// <param name="vm"></param>
        /// <returns></returns>
        private static ExecutionState Data_Set(RuntimeVM vm)
        {
            vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(2);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;

            var field = vm.PopString("field");
            var key = SmartContract.GetKeyForField(contractName, field, false);

            vm.Expect(vm.ContractDeployed(contractName), $"contract '{contractName}' is not deployed when trying to fetch field '{field}'");

            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.Transaction.Signatures.Length > 0, "No signatures found in transaction");

                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var obj = vm.Stack.Pop();
            var valBytes = obj.AsByteArray();

            var contractAddress = SmartContract.GetAddressFromContractName(contractName);
            if (vm.ProtocolVersion <= 12)
            {
                vm.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.WriteData), contractAddress, key, valBytes);
            }
            else
            {
                vm.WriteData(contractAddress, key, valBytes);
            }

            return ExecutionState.Running;
        }

        /// <summary>
        /// Delete a field in the current contract
        /// Can only be called from within a contract.
        /// </summary>
        /// <param name="vm"></param>
        /// <returns></returns>
        private static ExecutionState Data_Delete(RuntimeVM vm)
        {
            vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(1);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.Transaction.Signatures.Length > 0, "No signatures found in transaction");

                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var field = vm.PopString("field");
            var key = SmartContract.GetKeyForField(contractName, field, false);

            var contractAddress = SmartContract.GetAddressFromContractName(contractName);

            if (vm.ProtocolVersion <= 12)
            {
                vm.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.DeleteData), contractAddress,
                    key);
            }
            else
            {
                vm.DeleteData(contractAddress, key);
            }

            return ExecutionState.Running;
        }
        #endregion

        #region MAP
        private static ExecutionState Map_Has(RuntimeVM vm)
        {
            //vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(4);

            var contractName = vm.PopString("contract");
            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");
            }

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var entryKey = vm.Stack.Pop().AsByteArray();
            vm.Expect(entryKey.Length > 0, "invalid entry key");

            var type_obj = vm.Stack.Pop();
            var vmType = type_obj.AsEnum<VMType>();

            var map = new StorageMap(mapKey, vm.Storage);

            var keyExists = map.ContainsKey(entryKey);

            var val = new VMObject();
            val.SetValue(keyExists);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }


        private static ExecutionState Map_Get(RuntimeVM vm)
        {
            //vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(4);

            var contractName = vm.PopString("contract");
            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");
            }

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var entryKey = vm.Stack.Pop().AsByteArray();
            vm.Expect(entryKey.Length > 0, "invalid entry key");

            var type_obj = vm.Stack.Pop();
            var vmType = type_obj.AsEnum<VMType>();

            var map = new StorageMap(mapKey, vm.Storage);

            var value_bytes = map.GetRaw(entryKey);

            var val = new VMObject();

            if (value_bytes == null)
            {
                val.SetDefaultValue(vmType);
            }
            else
            {
                val.SetValue(value_bytes, vmType);
            }
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Set(RuntimeVM vm)
        {
            vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(3);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var entry_obj = vm.Stack.Pop();
            var entryKey = entry_obj.AsByteArray();
            vm.Expect(entryKey.Length > 0, "invalid entry key");

            var value = vm.Stack.Pop();

            var map = new StorageMap(mapKey, vm.Storage);

            var value_bytes = value.AsByteArray();

            map.SetRaw(entryKey, value_bytes);

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Remove(RuntimeVM vm)
        {
            var contextName = vm.CurrentContext.Name;
            vm.Expect(contextName != VirtualMachine.EntryContextName, $"Not allowed from entry context");

            vm.ExpectStackSize(2);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = contextName;
            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var entry_obj = vm.Stack.Pop();
            var entryKey = entry_obj.AsByteArray();
            vm.Expect(entryKey.Length > 0, "invalid entry key");

            var map = new StorageMap(mapKey, vm.Storage);

            map.Remove(entryKey);

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Clear(RuntimeVM vm)
        {
            vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(1);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var map = new StorageMap(mapKey, vm.Storage);
            map.Clear();

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Keys(RuntimeVM vm)
        {
            var contextName = vm.CurrentContext.Name;
            vm.Expect(contextName != VirtualMachine.EntryContextName, $"Not allowed from entry context");

            vm.ExpectStackSize(2);

            var contractName = vm.PopString("contract");
            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var map = new StorageMap(mapKey, vm.Storage);

            var keys = map.AllKeys<byte[]>();
            var val = VMObject.FromObject(keys);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Count(RuntimeVM vm)
        {
            //vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(2);

            var contractName = vm.PopString("contract");
            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");
            }

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var map = new StorageMap(mapKey, vm.Storage);

            var count = map.Count();
            var val = VMObject.FromObject(count);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }
        #endregion

        #region LIST
        private static ExecutionState List_Get(RuntimeVM vm)
        {
            //vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(4);

            var contractName = vm.PopString("contract");
            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");
            }

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var index = vm.PopNumber("index");
            vm.Expect(index >= 0, "invalid index");

            var type_obj = vm.Stack.Pop();
            var vmType = type_obj.AsEnum<VMType>();

            var list = new StorageList(listKey, vm.Storage);

            var value_bytes = list.GetRaw(index);

            var val = new VMObject();

            if (value_bytes == null)
            {
                val.SetDefaultValue(vmType);
            }
            else
            {
                val.SetValue(value_bytes, vmType);
            }
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }

        private static ExecutionState List_Add(RuntimeVM vm)
        {
            vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(2);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var value = vm.Stack.Pop();

            var list = new StorageList(listKey, vm.Storage);

            var value_bytes = value.AsByteArray();

            list.AddRaw(value_bytes);

            return ExecutionState.Running;
        }

        private static ExecutionState List_Replace(RuntimeVM vm)
        {
            vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(3);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var index = vm.PopNumber("index");
            vm.Expect(index >= 0, "invalid index");

            var value = vm.Stack.Pop();

            var list = new StorageList(listKey, vm.Storage);

            var value_bytes = value.AsByteArray();

            list.ReplaceRaw(index, value_bytes);

            return ExecutionState.Running;
        }

        private static ExecutionState List_RemoveAt(RuntimeVM vm)
        {
            vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(2);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;

            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var index = vm.PopNumber("index");
            vm.Expect(index >= 0, "invalid index");

            var list = new StorageList(listKey, vm.Storage);

            list.RemoveAt(index);

            return ExecutionState.Running;
        }

        private static ExecutionState List_Clear(RuntimeVM vm)
        {
            var contextName = vm.CurrentContext.Name;
            vm.Expect(contextName != VirtualMachine.EntryContextName, $"Not allowed from entry context");

            vm.ExpectStackSize(1);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = contextName;
            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), $"contract {contractName} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(contractName.ToLower()), $"contract {contractName} is not allowed to use this function");
            }

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var list = new StorageList(listKey, vm.Storage);
            list.Clear();

            return ExecutionState.Running;
        }

        private static ExecutionState List_Count(RuntimeVM vm)
        {
            //vm.Expect(!vm.IsEntryContext(vm.CurrentContext), $"Not allowed from this context");

            vm.ExpectStackSize(2);

            var contractName = vm.PopString("contract");
            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");
            }

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var list = new StorageList(listKey, vm.Storage);

            var count = list.Count();
            var val = VMObject.FromObject(count);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }
        #endregion

        #region Token Interactions
        private static ExecutionState Runtime_GetBalance(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            var source = vm.PopAddress();
            var symbol = vm.PopString("symbol");

            var balance = vm.GetBalance(symbol, source);

            var result = new VMObject();
            result.SetValue(balance);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferTokens(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            var symbol = vm.PopString("symbol");
            var amount = vm.PopNumber("amount");

            // Add Validations here
            if (vm.ProtocolVersion >= 13)
            {
                vm.ValidateBasicTransfer(source, destination, symbol, amount);
            }

            vm.TransferTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferBalance(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            var symbol = vm.PopString("symbol");

            var token = vm.GetToken(symbol);
            vm.Expect(token.IsFungible(), "must be fungible");

            var amount = vm.GetBalance(symbol, source);

            vm.TransferTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_SwapTokens(RuntimeVM vm)
        {
            vm.ExpectStackSize(5);

            vm.Expect(vm.ProtocolVersion < 13, "this method is obsolete");

            VMObject temp;

            temp = vm.Stack.Pop();
            vm.Expect(temp.Type == VMType.String, "expected string for target chain");
            var targetChain = temp.AsString();

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            temp = vm.Stack.Pop();
            vm.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var value = vm.PopNumber("amount");

            vm.SwapTokens(vm.Chain.Name, source, targetChain, destination, symbol, value);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_MintTokens(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);
            var hasGenesis = vm.HasGenesis;

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            var symbol = vm.PopString("symbol");

            if (vm.IsSystemToken(symbol))
            {
                if (hasGenesis)
                {
                    if (vm.ProtocolVersion <= 8)
                    {
                        throw new VMException(vm, $"Minting token {symbol} not allowed from this context");
                    }
                    else
                    {
                        vm.ExpectWarning(vm.IsPrimaryValidator(source), "only primary validator can mint system tokens", source);
                    }
                }
            }
            else
            {
                var tokenContext = vm.FindContext(symbol);

                // TODO review this
                if (tokenContext.Name != vm.CurrentContext.Name && vm.NexusName == DomainSettings.NexusMainnet)
                {
                    throw new VMException(vm, $"Minting token {symbol} not allowed from this context");
                }
            }

            var amount = vm.PopNumber("amount");

            if (vm.HasGenesis)
            {
                if (vm.ProtocolVersion > 8)
                {
                    if (symbol != DomainSettings.StakingTokenSymbol && symbol != DomainSettings.FuelTokenSymbol &&
                        symbol != DomainSettings.FuelTokenSymbol)
                    {
                        var isMinter = vm.IsMintingAddress(source, symbol);
                        vm.Expect(isMinter, $"{source} is not a valid minting address for {symbol}");
                    }
                }
                else
                {
                    var isMinter = vm.IsMintingAddress(source, symbol);
                    vm.Expect(isMinter, $"{source} is not a valid minting address for {symbol}");
                }

            }

            vm.MintTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_BurnTokens(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var target = vm.PopAddress();
            var symbol = vm.PopString("symbol");

            if (!vm.IsSystemToken(symbol))
            {
                var tokenContext = vm.FindContext(symbol);

                if (vm.GetGovernanceValue(Nexus.NexusProtocolVersionTag) <= 8)
                {
                    if (tokenContext.Name != vm.CurrentContext.Name)
                    {
                        throw new VMException(vm, $"Burning token {symbol} not allowed from this context");
                    }
                }
            }

            var amount = vm.PopNumber("amount");

            vm.BurnTokens(symbol, target, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            VMObject temp;

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            temp = vm.Stack.Pop();
            vm.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var tokenID = vm.PopNumber("token ID");

            vm.TransferToken(symbol, source, destination, tokenID);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_MintToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(6);

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            var symbol = vm.PopString("symbol");

            if (vm.IsSystemToken(symbol))
            {
                throw new VMException(vm, $"Minting system token {symbol} not allowed");
            }

            var tokenContext = vm.FindContext(symbol);

            // TODO review this
            if (tokenContext.Name != vm.CurrentContext.Name && vm.NexusName == DomainSettings.NexusMainnet)
            {
                throw new VMException(vm, $"Minting token {symbol} not allowed from this context");
            }

            var rom = vm.PopBytes("rom");
            var ram = vm.PopBytes("ram");

            BigInteger seriesID;

            seriesID = vm.PopNumber("series");

            Address creator = source;

            var tokenID = vm.MintToken(symbol, creator, destination, rom, ram, seriesID);

            var result = new VMObject();
            result.SetValue(tokenID);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_BurnToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var source = vm.PopAddress();
            var symbol = vm.PopString("symbol");
            var tokenID = vm.PopNumber("token ID");

            var tokenContext = vm.FindContext(symbol);
            var contractAddress = SmartContract.GetAddressFromContractName(symbol);
            var deployed = vm.Chain.IsContractDeployed(vm.Storage, contractAddress);

            if (vm.ProtocolVersion <= 8)
            {
                if (tokenContext.Name != vm.CurrentContext.Name && vm.NexusName == DomainSettings.NexusMainnet)
                {
                    vm.ExpectWarning(false, $"Tried to burn {symbol} tokens from this context {vm.CurrentContext.Name}", source);
                }
            }
            else
            {
                vm.Expect(deployed, $"{symbol} does not exist");
                if (vm.ProtocolVersion <= 12)
                {
                    if (Nexus.IsDangerousSymbol(symbol))
                    {
                        if (!(symbol == DomainSettings.LiquidityTokenSymbol &&
                              (vm.CurrentContext.Name == DomainSettings.LiquidityTokenSymbol
                               || vm.CurrentContext.Name == NativeContractKind.Exchange.GetContractName())))
                        {
                            vm.ExpectWarning(false, $"Tried to burn LP tokens from this context {vm.CurrentContext.Name}", source);
                        }
                    }
                }
                else
                {
                    if (symbol == DomainSettings.LiquidityTokenSymbol)
                    {
                        if (vm.CurrentContext.Name == DomainSettings.LiquidityTokenSymbol
                             || vm.CurrentContext.Name == NativeContractKind.Exchange.GetContractName())
                        {
                            vm.ExpectWarning(false, $"Tried to burn LP tokens from this context {vm.CurrentContext.Name}", source);
                        }
                    }
                    // Do nothing. We allow burning of any token from any context.
                }
            }

            vm.BurnToken(symbol, source, tokenID);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_InfuseToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(5);

            var source = vm.PopAddress();
            var targetSymbol = vm.PopString("target symbol");
            var tokenID = vm.PopNumber("token ID");
            var infuseSymbol = vm.PopString("infuse symbol");
            var value = vm.PopNumber("value");

            vm.InfuseToken(targetSymbol, source, tokenID, infuseSymbol, value);

            return ExecutionState.Running;
        }

        private static TokenContent Runtime_ReadTokenInternal(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            var symbol = vm.PopString("symbol");
            var tokenID = vm.PopNumber("token ID");

            var result = vm.ReadToken(symbol, tokenID);

            vm.Expect(result.TokenID == tokenID, "retrived NFT content does not have proper tokenID");

            return result;
        }

        private static ExecutionState Runtime_ReadToken(RuntimeVM vm)
        {
            var content = Runtime_ReadTokenInternal(vm);

            var fieldList = vm.PopString("fields").Split(',');

            var result = new VMObject();

            var fields = new Dictionary<VMObject, VMObject>();
            foreach (var field in fieldList)
            {
                object obj;

                switch (field)
                {
                    case "chain": obj = content.CurrentChain; break;
                    case "owner": obj = content.CurrentOwner.Text; break;
                    case "creator": obj = content.Creator.Text; break;
                    case "ROM": obj = content.ROM; break;
                    case "RAM": obj = content.RAM; break;
                    case "tokenID": obj = content.TokenID; break;
                    case "seriesID": obj = content.SeriesID; break;
                    case "mintID": obj = content.MintID; break;

                    default:
                        throw new VMException(vm, "unknown nft field: " + field);
                }

                var key = VMObject.FromObject(field);
                fields[key] = VMObject.FromObject(obj);
            }

            result.SetValue(fields);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_ReadTokenRAM(RuntimeVM Runtime)
        {
            var content = Runtime_ReadTokenInternal(Runtime);

            var result = new VMObject();
            result.SetValue(content.RAM, VMType.Bytes);
            Runtime.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_ReadTokenROM(RuntimeVM Runtime)
        {
            var content = Runtime_ReadTokenInternal(Runtime);

            var result = new VMObject();
            result.SetValue(content.ROM, VMType.Bytes);
            Runtime.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_WriteToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            Address from;

            from = vm.PopAddress();

            var symbol = vm.PopString("symbol");
            var tokenID = vm.PopNumber("token ID");
            var ram = vm.PopBytes("ram");

            /*if (symbol != vm.CurrentContext.Name)
            {
                throw new VMException(vm, $"Write token {symbol} not allowed from this context");
            }*/

            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(!Nexus.IsDangerousSymbol(symbol.ToUpper()), $"contract {symbol} is not allowed to use this function");
                vm.Expect(!Nexus.IsNativeContract(symbol.ToLower()), $"contract {symbol} is not allowed to use this function");
            }

            vm.WriteToken(from, symbol, tokenID, ram);

            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_GetGovernanceValue(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);
            var tag = vm.PopString("tag");

            var val = vm.Nexus.GetGovernanceValue(vm.RootStorage, tag);

            var result = new VMObject();
            result.SetValue(val);
            vm.Stack.Push(result);
            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_CreateTokenSeries(RuntimeVM vm)
        {
            vm.ExpectStackSize(7);

            var from = vm.PopAddress();
            var symbol = vm.PopString("symbol");
            var seriesID = vm.PopNumber("series ID");
            var maxSupply = vm.PopNumber("max supply");
            var mode = vm.PopEnum<TokenSeriesMode>("mode");
            var script = vm.PopBytes("script");
            var abiBytes = vm.PopBytes("abi bytes");

            /*if (symbol != vm.CurrentContext.Name)
            {
                throw new VMException(vm, $"Creating token series {symbol} not allowed from this context");
            }*/

            var abi = ContractInterface.FromBytes(abiBytes);

            vm.CreateTokenSeries(symbol, from, seriesID, maxSupply, mode, script, abi);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TokenExists(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var temp = vm.Stack.Pop();
            vm.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var success = vm.TokenExists(symbol);

            var result = new VMObject();
            result.SetValue(success);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TokenGetDecimals(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var symbol = vm.PopString("symbol");

            if (!vm.TokenExists(symbol))
            {
                return ExecutionState.Fault;
            }

            var token = vm.GetToken(symbol);

            var result = new VMObject();
            result.SetValue(token.Decimals);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TokenGetFlags(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var symbol = vm.PopString("symbol");

            if (!vm.TokenExists(symbol))
            {
                return ExecutionState.Fault;
            }

            var token = vm.GetToken(symbol);

            var result = new VMObject();
            result.SetValue(token.Flags);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }
        #endregion

        #region Contract / Token Deployment
        private static ExecutionState Runtime_DeployContract(RuntimeVM vm)
        {
            var tx = vm.Transaction;
            Throw.IfNull(tx, nameof(tx));

            if (vm.HasGenesis)
            {
                var pow = tx.Hash.GetDifficulty();
                vm.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");
            }

            vm.ExpectStackSize(4);

            var from = vm.PopAddress();
            vm.Expect(from.IsUser, "address must be user");

            if (vm.HasGenesis)
            {
                //Runtime.Expect(org != DomainSettings.ValidatorsOrganizationName, "cannot deploy contract via this organization");
                vm.Expect(vm.IsStakeMaster(from), "needs to be master");
            }

            vm.Expect(vm.IsWitness(from), "invalid witness");

            var contractName = vm.PopString("contractName");

            var contractAddress = SmartContract.GetAddressFromContractName(contractName);
            var deployed = vm.Chain.IsContractDeployed(vm.Storage, contractAddress);
            vm.ExpectWarning(!deployed, $"{contractName} is already deployed", from);

            // check if exists token with same name
            var tokenExists = vm.TokenExists(contractName.ToUpper());
            vm.ExpectWarning(!tokenExists, $"token with name {contractName} already exists", from);

            byte[] script;
            ContractInterface abi;

            script = vm.PopBytes("contractScript");

            var abiBytes = vm.PopBytes("contractABI");

            bool isNative = Nexus.IsNativeContract(contractName);
            if (isNative)
            {
                /*if (contractName == "validator" && vm.GenesisAddress == Address.Null)
                {
                    vm.Nexus.BeginInitialize(vm, from);
                }*/

                vm.Expect(script.Length == 1 && script[0] == (byte)Opcode.RET, "invalid script for native contract");
                vm.Expect(abiBytes.Length == 0, "invalid abi for native contract");

                var contractInstance = NativeContract.GetNativeContractByAddress(contractAddress);
                abi = contractInstance.ABI;
            }
            else
            {
                if (ValidationUtils.IsValidTicker(contractName))
                {
                    throw new VMException(vm, "use createToken instead for this kind of contract");
                }
                else
                {
                    vm.Expect(ValidationUtils.IsValidIdentifier(contractName), "invalid contract name");
                }

                var isReserved = ValidationUtils.IsReservedIdentifier(contractName);

                // TODO support reserved names
                /*if (isReserved && vm.IsWitness(vm.GenesisAddress))
                {
                    isReserved = false;
                }*/

                vm.ExpectWarning(!isReserved, $"name '{contractName}' reserved by system", from);

                abi = ContractInterface.FromBytes(abiBytes);

                vm.Expect(abi.Methods.Any(), "contract must have at least one public method");

                var fuelCost = vm.GetGovernanceValue(DomainSettings.FuelPerContractDeployTag);
                // governance value is in usd fiat, here convert from fiat to fuel amount
                fuelCost = vm.GetTokenQuote(DomainSettings.FiatTokenSymbol, DomainSettings.FuelTokenSymbol, fuelCost);

                // burn the "cost" tokens
                vm.BurnTokens(DomainSettings.FuelTokenSymbol, from, fuelCost);
            }

            // ABI validation
            ValidateABI(vm, contractName, abi, isNative);

            var success = vm.Chain.DeployContractScript(vm.Storage, from, contractName, contractAddress, script, abi);
            vm.Expect(success, $"deployment of {contractName} failed");

            var constructor = abi.FindMethod(SmartContract.ConstructorName);

            if (constructor != null)
            {
                vm.CallContext(contractName, constructor, from);
            }

            vm.Notify(EventKind.ContractDeploy, from, contractName);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_UpgradeContract(RuntimeVM vm)
        {
            var tx = vm.Transaction;
            Throw.IfNull(tx, nameof(tx));

            var pow = tx.Hash.GetDifficulty();
            vm.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            vm.ExpectStackSize(4);

            var from = vm.PopAddress();
            vm.Expect(from.IsUser, "address must be user");

            vm.Expect(vm.IsStakeMaster(from), "needs to be master");

            vm.ExpectWarning(vm.IsWitness(from), "invalid witness", from);

            var contractName = vm.PopString("contractName");

            var contractAddress = SmartContract.GetAddressFromContractName(contractName);
            var deployed = vm.Chain.IsContractDeployed(vm.Storage, contractAddress);

            vm.Expect(deployed, $"{contractName} does not exist");

            byte[] script;
            ContractInterface abi;

            bool isNative = Nexus.IsNativeContract(contractName);
            vm.ExpectWarning(!isNative, "cannot upgrade native contract", from);

            if (vm.ProtocolVersion >= 13)
            {
                vm.Expect(!Nexus.IsDangerousSymbol(contractName.ToUpper()), "cannot upgrade dangerous symbol");
            }

            bool isToken = ValidationUtils.IsValidTicker(contractName);

            script = vm.PopBytes("contractScript");

            var abiBytes = vm.PopBytes("contractABI");
            abi = ContractInterface.FromBytes(abiBytes);

            // ABI validation
            ValidateABI(vm, contractName, abi, isNative);

            SmartContract oldContract;
            if (isToken)
            {
                oldContract = vm.Nexus.GetTokenContract(vm.Storage, contractName);
            }
            else
            {
                oldContract = vm.Chain.GetContractByName(vm.Storage, contractName);
            }

            vm.Expect(oldContract != null, "could not fetch previous contract");
            vm.Expect(abi.Implements(oldContract.ABI), "new abi does not implement all methods of previous abi");

            var triggerName = AccountTrigger.OnUpgrade.ToString();
            vm.ValidateTriggerGuard($"{contractName}.{triggerName}");

            vm.ExpectWarning(vm.InvokeTrigger(false, script, contractName, abi, triggerName, from) == TriggerResult.Success, triggerName + " trigger failed", from);

            if (isToken)
            {
                vm.Nexus.UpgradeTokenContract(vm.RootStorage, contractName, script, abi);
            }
            else
            {
                vm.Chain.UpgradeContract(vm.Storage, contractName, script, abi);
            }

            vm.Notify(EventKind.ContractUpgrade, from, contractName);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_KillContract(RuntimeVM vm)
        {
            var tx = vm.Transaction;
            Throw.IfNull(tx, nameof(tx));

            var pow = tx.Hash.GetDifficulty();
            vm.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            vm.ExpectStackSize(2);

            var from = vm.PopAddress();
            vm.Expect(from.IsUser, "address must be user");

            vm.Expect(vm.IsWitness(from), "invalid witness");

            var contractName = vm.PopString("contractName");

            var contractAddress = SmartContract.GetAddressFromContractName(contractName);
            var deployed = vm.Chain.IsContractDeployed(vm.Storage, contractAddress);

            vm.Expect(deployed, $"{contractName} does not exist");

            bool isNative = Nexus.IsNativeContract(contractName);
            vm.ExpectWarning(!isNative, "cannot kill native contract", from);

            bool isToken = ValidationUtils.IsValidTicker(contractName);
            vm.ExpectWarning(!isToken, "cannot kill token contract", from);

            var contractOwner = vm.GetContractOwner(contractAddress);
            vm.ExpectWarning(contractOwner == from, "only contract owner can kill contract", from);

            SmartContract contract;
            if (isToken)
            {
                contract = vm.Nexus.GetTokenContract(vm.Storage, contractName);
            }
            else
            {
                contract = vm.Chain.GetContractByName(vm.Storage, contractName);
            }

            vm.Expect(contract != null, "could not fetch previous contract");

            var customContract = contract as CustomContract;
            vm.Expect(customContract != null, "could not contract script");

            var abi = contract.ABI;
            var triggerName = AccountTrigger.OnKill.ToString();

            vm.ValidateTriggerGuard($"{contractName}.{triggerName}");

            var triggerResult = vm.InvokeTrigger(false, customContract.Script, contract.Name, contract.ABI, triggerName,
                new object[] { from });
            if (contractName == DomainSettings.LiquidityTokenSymbol)
            {
                vm.ExpectWarning(triggerResult == TriggerResult.Success || triggerResult == TriggerResult.Missing, triggerName + " trigger failed", from);
            }
            else
            {
                vm.ExpectWarning(triggerResult == TriggerResult.Success, triggerName + " trigger failed", from);
            }

            if (isToken)
            {
                throw new ChainException("Cannot kill token contract (missing implementation?)");
            }
            else
            {
                vm.Chain.KillContract(vm.Storage, contractName);
            }

            vm.Notify(EventKind.ContractKill, from, contractName);

            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_CreateToken(RuntimeVM vm)
        {
            Address owner = Address.Null;
            string symbol = null;
            string name = null;
            BigInteger maxSupply = -1;
            int decimals = -1;
            TokenFlags flags = TokenFlags.None;

            vm.ExpectStackSize(3);

            owner = vm.PopAddress();

            var script = vm.PopBytes("script");

            ContractInterface abi;

            var abiBytes = vm.PopBytes("abi bytes");
            abi = ContractInterface.FromBytes(abiBytes);

            vm.Expect(abi.HasTokenTrigger(TokenTrigger.OnMint), $"Token contract needs to implement {TokenTrigger.OnMint}");

            var rootChain = (Chain)vm.GetRootChain(); // this cast is not the best, but works for now...
            var storage = vm.RootStorage;

            TokenUtils.FetchProperty(storage, rootChain, "getSymbol", script, abi, (prop, value) =>
            {
                symbol = value.AsString();
            });

            TokenUtils.FetchProperty(storage, rootChain, "getName", script, abi, (prop, value) =>
            {
                name = value.AsString();
            });

            TokenUtils.FetchProperty(storage, rootChain, "getTokenFlags", script, abi, (prop, value) =>
            {
                flags = value.AsEnum<TokenFlags>();
            });

            // we offer two ways to describe the flags, either individually or via getTokenFlags
            if (flags == TokenFlags.None)
            {
                var possibleFlags = Enum.GetValues(typeof(TokenFlags)).Cast<TokenFlags>().ToArray();
                foreach (var entry in possibleFlags)
                {
                    var flag = entry; // this line necessary for lambda closure to catch the correct value
                    var propName = $"is{flag}";

                    // for each flag, if the property exists and returns true, we set the flag
                    TokenUtils.FetchProperty(storage, rootChain, propName, script, abi, (prop, value) =>
                    {
                        var isSet = value.AsBool();
                        if (isSet)
                        {
                            flags |= flag;
                        }
                    });
                }
            }

            /*if (flags.HasFlag(TokenFlags.Burnable))
            {
                vm.Expect(abi.HasMethod(TokenUtils.BurnMethodName), "Token contract has to implement a burn method");
            }

            if (flags.HasFlag(TokenFlags.Mintable))
            {
                vm.Expect(abi.HasMethod(TokenUtils.MintMethodName), "Token contract has to implement a mint method");
            }*/

            if (flags.HasFlag(TokenFlags.Finite))
            {
                TokenUtils.FetchProperty(storage, rootChain, "getMaxSupply", script, abi, (prop, value) =>
                {
                    maxSupply = value.AsNumber();
                });
            }
            else
            {
                maxSupply = 0;
            }

            if (flags.HasFlag(TokenFlags.Fungible))
            {
                TokenUtils.FetchProperty(storage, rootChain, "getDecimals", script, abi, (prop, value) =>
                {
                    decimals = (int)value.AsNumber();
                });
            }
            else
            {
                decimals = 0;
            }

            // check if contract already exists
            var contractAddress = SmartContract.GetAddressFromContractName(symbol.ToLower());
            var deployed = vm.Chain.IsContractDeployed(vm.Storage, contractAddress);
            vm.ExpectWarning(!deployed, $"{symbol} already exists", owner);
            vm.Expect(ValidationUtils.IsValidTicker(symbol), "missing or invalid token symbol");
            vm.Expect(!string.IsNullOrEmpty(name), "missing or invalid token name");
            vm.Expect(maxSupply >= 0, "missing or invalid token supply");
            vm.Expect(decimals >= 0, "missing or invalid token decimals");
            vm.Expect(flags != TokenFlags.None, "missing or invalid token flags");

            vm.Expect(!flags.HasFlag(TokenFlags.Swappable), "swappable swap can't be set in token creation");

            vm.CreateToken(owner, symbol, name, maxSupply, decimals, flags, script, abi);

            return ExecutionState.Running;
        }
        #endregion

        #region CHAIN

        private static ExecutionState Nexus_BeginInit(RuntimeVM vm)
        {
            vm.Expect(!vm.HasGenesis, "nexus already initialized");

            vm.ExpectStackSize(1);

            var owner = vm.PopAddress();

            vm.Nexus.BeginInitialize(vm, owner);

            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_EndInit(RuntimeVM vm)
        {
            vm.Expect(!vm.HasGenesis, "nexus already initialized");

            vm.ExpectStackSize(1);

            var owner = vm.PopAddress();

            vm.Nexus.FinishInitialize(vm, owner);

            return ExecutionState.Running;
        }

        //private static ExecutionState Nexus_CreateChain(RuntimeVM vm)
        //{
        //    vm.ExpectStackSize(4);

        //    var source = vm.PopAddress();
        //    var org = vm.PopString("organization");
        //    var name = vm.PopString("name");
        //    var parentName = vm.PopString("parent");

        //    vm.CreateChain(source, org, name, parentName);

        //    return ExecutionState.Running;
        //}

        //private static ExecutionState Nexus_CreatePlatform(RuntimeVM vm)
        //{
        //    vm.ExpectStackSize(5);

        //    var source = vm.PopAddress();
        //    var name = vm.PopString("name");
        //    var externalAddress = vm.PopString("external address");
        //    var interopAddress = vm.PopAddress();
        //    var symbol = vm.PopString("symbol");

        //    var target = vm.CreatePlatform(source, name, externalAddress, interopAddress, symbol);

        //    var result = new VMObject();
        //    result.SetValue(target);
        //    vm.Stack.Push(result);

        //    return ExecutionState.Running;
        //}

        //private static ExecutionState Nexus_SetPlatformTokenHash(RuntimeVM vm)
        //{
        //    vm.ExpectStackSize(3);

        //    var symbol = vm.PopString("symbol");
        //    var platform = vm.PopString("platform");

        //    var bytes = vm.PopBytes("hash");
        //    var hash = new Hash(bytes.Skip(1).ToArray());

        //    vm.SetPlatformTokenHash(symbol, platform, hash);

        //    return ExecutionState.Running;
        //}

        #endregion

        #region Organization
        private static ExecutionState Nexus_CreateOrganization(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            var source = vm.PopAddress();
            var ID = vm.PopString("id");
            var name = vm.PopString("name");
            //var ID = (new BigInteger(vm.Transaction.Hash.ToByteArray().Concat(Encoding.UTF8.GetBytes(name)).ToArray())).ToString();
            var script = vm.PopBytes("script");

            if (vm.ProtocolVersion >= 10)
            {
                var contractAddress = SmartContract.GetAddressFromContractName(ID.ToLower());
                var deployed = vm.Chain.IsContractDeployed(vm.Storage, contractAddress);
                vm.ExpectWarning(!deployed, $"{ID} already exists", source);

                bool isNative = Nexus.IsNativeContract(ID.ToLower());
                vm.ExpectWarning(!isNative, "cannot create org with the same name as a native contract", source);

                bool isToken = ValidationUtils.IsValidTicker(ID.ToUpper());
                vm.ExpectWarning(!isToken, "cannot create org with the same name as a  token contract", source);
            }


            vm.CreateOrganization(source, ID, name, script);

            return ExecutionState.Running;
        }

        private static ExecutionState Organization_AddMember(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var source = vm.PopAddress();
            var name = vm.PopString("name");
            var target = vm.PopAddress();

            vm.AddMember(name, source, target);

            return ExecutionState.Running;
        }

        private static ExecutionState Organization_RemoveMember(RuntimeVM vm)
        {
            if (vm.ProtocolVersion <= 9) return ExecutionState.Fault;

            vm.ExpectStackSize(3);

            var source = vm.PopAddress();
            var name = vm.PopString("name");
            var target = vm.PopAddress();

            vm.RemoveMember(name, source, target);

            return ExecutionState.Running;
        }
        #endregion

        private static void ValidateABI(RuntimeVM vm, string contractName, ContractInterface abi, bool isNative)
        {
            var offsets = new HashSet<int>();
            var names = new HashSet<string>();
            foreach (var method in abi.Methods)
            {
                vm.Expect(ValidationUtils.IsValidMethod(method.name, method.returnType), "invalid method: " + method.name);
                var normalizedName = method.name.ToLower();
                vm.Expect(!names.Contains(normalizedName), $"duplicated method name in {contractName}: {normalizedName}");

                names.Add(normalizedName);

                if (!isNative)
                {
                    vm.Expect(method.offset >= 0, $"invalid offset in {contractName} contract abi for method {method.name}");
                    vm.Expect(!offsets.Contains(method.offset), $"duplicated offset in {contractName} contract abi for method {method.name}");
                    offsets.Add(method.offset);
                }
            }
        }

        #region TASKS
        private static ExecutionState Task_Current(RuntimeVM vm)
        {
            var result = new VMObject();
            result.SetValue(vm.CurrentTask);
            vm.Stack.Push(result);
            return ExecutionState.Running;
        }

        private static ExecutionState Task_Get(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var taskID = vm.PopNumber("task");
            var task = (ChainTask)vm.GetTask(taskID);

            var result = new VMObject();
            result.SetValue(task);
            vm.Stack.Push(result);
            return ExecutionState.Running;
        }

        private static ExecutionState Task_Stop(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            vm.Expect(vm.CurrentTask == null, "cannot stop task from within a task");

            var taskID = vm.PopNumber("task");
            var task = vm.GetTask(taskID);
            vm.Expect(task != null, "task not found");

            vm.StopTask(task);

            return ExecutionState.Running;
        }

        private static ExecutionState Task_Start(RuntimeVM vm)
        {
            vm.ExpectStackSize(7);

            var contractName = vm.PopString("contract");
            var methodBytes = vm.PopBytes("method bytes");
            var from = vm.PopAddress();
            var frequency = (uint)vm.PopNumber("frequency");
            var delay = (uint)vm.PopNumber("delay");
            var mode = vm.PopEnum<TaskFrequencyMode>("mode");
            var gasLimit = vm.PopNumber("gas limit");

            var method = ContractMethod.FromBytes(methodBytes);

            var task = vm.StartTask(from, contractName, method, frequency, delay, mode, gasLimit);

            var result = new VMObject();
            result.SetValue(task.ID);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        #endregion

        #region ACCOUNT 
        private static ExecutionState Account_Name(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var address = vm.PopAddress();

            var result = vm.GetAddressName(address);
            vm.Stack.Push(VMObject.FromObject(result));

            return ExecutionState.Running;
        }

        private static ExecutionState Account_Activity(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var address = vm.PopAddress();

            var result = vm.Chain.GetLastActivityOfAddress(address);
            vm.Stack.Push(VMObject.FromObject(result));

            return ExecutionState.Running;
        }

        private static ExecutionState Account_Transactions(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var address = vm.PopAddress();

            var result = vm.Chain.GetTransactionHashesForAddress(address);

            var dict = new Dictionary<VMObject, VMObject>();
            for (int i = 0; i < result.Length; i++)
            {
                var hash = result[i];
                var temp = new VMObject();
                temp.SetValue(hash);
                dict[VMObject.FromObject(i)] = temp;
            }

            var obj = new VMObject();
            obj.SetValue(dict);
            vm.Stack.Push(obj);

            return ExecutionState.Running;
        }

        #endregion

    }
}
