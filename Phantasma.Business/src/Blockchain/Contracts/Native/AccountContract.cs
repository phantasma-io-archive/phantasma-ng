using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Structs;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Triggers;
using Phantasma.Core.Domain.Triggers.Enums;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class AccountContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Account;

#pragma warning disable 0649
        internal StorageMap _addressMap; //<Address, string> 
        internal StorageMap _nameMap; //<string, Address> 
        internal StorageMap _scriptMap; //<Address, byte[]> 
        internal StorageMap _abiMap; //<Address, byte[]> 
#pragma warning restore 0649

        public static readonly BigInteger RegistrationCost = UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals);

        public AccountContract() : base()
        {
        }

        /// <summary>
        /// Method used to Register a name for a given address.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="name"></param>
        public void RegisterName(Address target, string name)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");
            Runtime.Expect(ValidationUtils.IsValidIdentifier(name), "invalid name");

            var stake = Runtime.GetStake(target);
            Runtime.Expect(stake >= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals), "must have something staked");

            Runtime.Expect(!Nexus.IsDangerousAddress(target), "this address can't be used as source");

            Runtime.Expect(name != Runtime.NexusName, "name already used for nexus");
            Runtime.Expect(!Runtime.ChainExists(name), "name already used for a chain");
            Runtime.Expect(!Runtime.PlatformExists(name), "name already used for a platform");
            Runtime.Expect(!Runtime.ContractExists(name), "name already used for a contract");
            Runtime.Expect(!Runtime.FeedExists(name), "name already used for a feed");
            Runtime.Expect(!Runtime.OrganizationExists(name), "name already used for a organization");
            Runtime.Expect(!Runtime.TokenExists(name.ToUpper()), "name already used for a token");

            Runtime.Expect(!_addressMap.ContainsKey(target), "address already has a name");
            Runtime.Expect(!_nameMap.ContainsKey(name), "name already used for other account");

            var isReserved = ValidationUtils.IsReservedIdentifier(name);

            /* // TODO reserved names not allowed for now 
            if (isReserved) 
            {
                var pollName = ConsensusContract.SystemPoll + name;
                var hasConsensus = Runtime.CallNativeContext(NativeContractKind.Consensus, "HasConsensus", pollName, name).AsBool();
                isReserved = false;
            }*/

            Runtime.Expect(!isReserved, $"name '{name}' reserved by system");

            _addressMap.Set(target, name);
            _nameMap.Set(name, target);

            Runtime.Notify(EventKind.AddressRegister, target, name);
        }

        /// <summary>
        /// Method used to unregisters a name for a given address.
        /// </summary>
        /// <param name="target"></param>
        public void UnregisterName(Address target)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");

            Runtime.Expect(!Nexus.IsDangerousAddress(target), "this address can't be used as source");

            Runtime.Expect(_addressMap.ContainsKey(target), "address doest not have a name yet");

            var name = _addressMap.Get<Address, string>(target);
            _addressMap.Remove(target);
            _nameMap.Remove(name);

            Runtime.Notify(EventKind.AddressUnregister, target, name);
        }

        /// <summary>
        /// Method used to Register a script for a given address.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="script"></param>
        /// <param name="abiBytes"></param>
        public void RegisterScript(Address target, byte[] script, byte[] abiBytes)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");

            Runtime.Expect(!Nexus.IsDangerousAddress(target), "this address can't be used as source");

            var stake = Runtime.GetStake(target);
            Runtime.Expect(stake >= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals), "must have something staked");

            Runtime.Expect(script.Length < 1024, "invalid script length");

            Runtime.Expect(!_scriptMap.ContainsKey(target), "address already has a script");

            var abi = ContractInterface.FromBytes(abiBytes);
            Runtime.Expect(abi.MethodCount > 0, "unexpected empty contract abi");

            var witnessTriggerName = nameof(ContractTrigger.OnWitness);
            if (abi.HasMethod(witnessTriggerName))
            {
                var contextName = target.Text;

                var witnessCheck = Runtime.InvokeTrigger(false, script, contextName, abi, witnessTriggerName, Address.Null) != TriggerResult.Failure;
                Runtime.Expect(!witnessCheck, "script does not handle OnWitness correctly, case #1");

                witnessCheck = Runtime.InvokeTrigger(false, script, contextName, abi, witnessTriggerName, target) != TriggerResult.Failure;
                Runtime.Expect(witnessCheck, "script does not handle OnWitness correctly, case #2");
            }

            _scriptMap.Set(target, script);
            _abiMap.Set(target, abiBytes);

            var constructor = abi.FindMethod(ConstructorName);

            if (constructor != null)
            {
                Runtime.CallContext(target.Text, constructor, target);
            }

            // TODO? Runtime.Notify(EventKind.AddressRegister, target, script);
        }

        /// <summary>
        /// Checks if a address has a script
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool HasScript(Address address)
        {
            if (address.IsUser)
            {
                return _scriptMap.ContainsKey(address);
            }

            return false;
        }

        /// <summary>
        /// Method used to retrive an address name.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public string LookUpAddress(Address target)
        {
            if (_addressMap.ContainsKey(target))
            {
                return _addressMap.Get<Address, string>(target);
            }

            return ValidationUtils.ANONYMOUS_NAME;
        }

        /// <summary>
        /// Returns a script for a given address.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public byte[] LookUpScript(Address target)
        {
            if (_scriptMap.ContainsKey(target))
            {
                return _scriptMap.Get<Address, byte[]>(target);
            }

            return Array.Empty<byte>();
        }

        /// <summary>
        /// Returns the ABI for a given address.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public byte[] LookUpABI(Address target)
        {
            if (_abiMap.ContainsKey(target))
            {
                return _abiMap.Get<Address, byte[]>(target);
            }

            return Array.Empty<byte>();
        }

        /// <summary>
        /// Returns the Address for a given name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Address LookUpName(string name)
        {
            if (name == ValidationUtils.ANONYMOUS_NAME || name == ValidationUtils.NULL_NAME)
            {
                return Address.Null;
            }

            if (_nameMap.ContainsKey(name))
            {
                return _nameMap.Get<string, Address>(name);
            }

            return Address.Null;
        }

        /// <summary>
        /// Migrate from on address to another.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="target"></param>
        /// <exception cref="ChainException"></exception>
        public void Migrate(Address from, Address target)
        {
            Runtime.Expect(target != from, "addresses must be different");
            Runtime.Expect(target.IsUser, "must be user address");

            Runtime.Expect(!Nexus.IsDangerousAddress(from), "this address can't be used as source");

            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            bool isSeller = Runtime.CallNativeContext(NativeContractKind.Market, nameof(MarketContract.IsSeller), from).AsBool();
            Runtime.Expect(!isSeller, "sale pending on market");

            isSeller = Runtime.CallNativeContext(NativeContractKind.Sale, nameof(SaleContract.IsSeller), from).AsBool();
            Runtime.Expect(!isSeller, "crowdsale pending");

            var relayBalance = Runtime.CallNativeContext(NativeContractKind.Relay, nameof(RelayContract.GetBalance), from).AsNumber();
            Runtime.Expect(relayBalance == 0, "relay channel can't be open");

            var unclaimed = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), from).AsNumber();
            if (unclaimed > 0)
            {
                Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Claim), from, from);
            }

            var symbols = Runtime.GetTokens();
            foreach (var symbol in symbols)
            {
                var balance = Runtime.GetBalance(symbol, from);
                if (balance > 0)
                {
                    var info = Runtime.GetToken(symbol);
                    if (info.IsFungible())
                    {
                        if (Runtime.ProtocolVersion <= 12)
                        {
                            Runtime.TransferTokens(symbol, from, target, balance);
                        }
                        else if (symbol != DomainSettings.FuelTokenSymbol)
                        {
                            Runtime.TransferTokens(symbol, from, target, balance);
                        }
                        else
                        {
                            if (balance > UnitConversion.GetUnitValue(DomainSettings.FuelTokenDecimals) * 2)
                                Runtime.TransferTokens(symbol, from, target, balance - UnitConversion.GetUnitValue(DomainSettings.FuelTokenDecimals));
                            else
                                throw new ChainException("Can't migrate address with less than 2 fuel");
                        }
                    }
                    else
                    {
                        var tokenIDs = Runtime.GetOwnerships(symbol, from);
                        foreach (var tokenID in tokenIDs)
                        {
                            Runtime.TransferToken(symbol, from, target, tokenID);
                        }
                    }
                }
            }

            if (_addressMap.ContainsKey(from))
            {
                var currentName = _addressMap.Get<Address, string>(from);
                _addressMap.Migrate<Address, string>(from, target);
                _nameMap.Set(currentName, target);

                var tmp = LookUpAddress(target);
                Runtime.Expect(tmp == currentName, "migration of name failed");
            }

            _scriptMap.Migrate<Address, byte[]>(from, target);
            _abiMap.Migrate<Address, byte[]>(from, target);

            var stake = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetStake), from).AsNumber();
            if (stake > 0)
            {
                Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Migrate), from, target);
            }

            if (Runtime.IsKnownValidator(from))
            {
                Runtime.CallNativeContext(NativeContractKind.Validator, nameof(ValidatorContract.Migrate), from, target);
            }

            var usedSpace = Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.GetUsedSpace), from).AsNumber();
            if (usedSpace > 0)
            {
                Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.Migrate), from, target);
            }

            Runtime.CallNativeContext(NativeContractKind.Consensus, nameof(ConsensusContract.Migrate), from, target);

            // TODO exchange, friend

            var orgs = Runtime.GetOrganizations();
            foreach (var orgID in orgs)
            {
                Runtime.MigrateMember(orgID, from, from, target);
            }

            var migrateMethod = new ContractMethod("OnMigrate", VMType.None, -1, new ContractParameter[] { new ContractParameter("from", VMType.Object), new ContractParameter("to", VMType.Object) });

            var contracts = Runtime.GetContracts();
            foreach (var contract in contracts)
            {
                var abi = contract.ABI;

                if (abi.Implements(migrateMethod))
                {
                    var method = abi.FindMethod(migrateMethod.name);
                    Runtime.CallContext(contract.Name, method, from, target);
                }
            }

            foreach (var symbol in symbols)
            {
                var token = Runtime.GetToken(symbol);

                var abi = token.ABI;

                if (abi.Implements(migrateMethod))
                {
                    var method = abi.FindMethod(migrateMethod.name);
                    Runtime.CallContext(symbol, method, from, target);
                }
            }

            Runtime.MigrateToken(from, target);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="trigger"></param>
        /// <returns></returns>
        public static ContractMethod GetTriggerForABI(ContractTrigger trigger)
        {
            return GetTriggersForABI(new[] { trigger }).First();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="triggers"></param>
        /// <returns></returns>
        public static IEnumerable<ContractMethod> GetTriggersForABI(IEnumerable<ContractTrigger> triggers)
        {
            var entries = new Dictionary<ContractTrigger, int>();
            foreach (var trigger in triggers)
            {
                entries[trigger] = 0;
            }

            return GetTriggersForABI(entries);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="triggers"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IEnumerable<ContractMethod> GetTriggersForABI(Dictionary<ContractTrigger, int> triggers)
        {
            var result = new List<ContractMethod>();

            foreach (var entry in triggers)
            {
                var trigger = entry.Key;
                var offset = entry.Value;

                switch (trigger)
                {
                    case ContractTrigger.OnWitness:
                    case ContractTrigger.OnUpgrade:
                        result.Add(new ContractMethod(trigger.ToString(), VMType.None, offset, new[] { new ContractParameter("from", VMType.Object) }));
                        break;

                    case ContractTrigger.OnBurn:
                    case ContractTrigger.OnMint:
                    case ContractTrigger.OnReceive:
                    case ContractTrigger.OnSend:
                        result.Add(new ContractMethod(trigger.ToString(), VMType.None, offset, new[] {
                            new ContractParameter("from", VMType.Object),
                            new ContractParameter("to", VMType.Object),
                            new ContractParameter("symbol", VMType.String),
                            new ContractParameter("amount", VMType.Number)
                        }));
                        break;

                    default:
                        throw new Exception("AddTriggerToABI: Unsupported trigger: " + trigger);
                }
            }

            return result;
        }
    }
}
