using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Business.VM;
using Phantasma.Core;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;

namespace Phantasma.Business.Blockchain;

public static class NexusExtensions
{
    internal static bool ValidateTransferSystem(this Nexus Nexus, IRuntime Runtime, Address source, Address destination, string symbol, BigInteger amount,
        out bool isOrganizationTransaction, Address _infusionOperationAddress )
    {
        isOrganizationTransaction = false;
        if (source.IsSystem)
        {
            var org = Nexus.GetOrganizationByAddress(Runtime.RootStorage, source);
            if (org != null)
            {
                if (Runtime.ProtocolVersion <= 8)
                {
                    Runtime.ExpectFiltered(org == null, "moving funds from orgs currently not possible", source);
                }
                else if ( Runtime.ProtocolVersion <= 9)
                {
                    Runtime.ExpectWarning(org != null, "moving funds from orgs currently not possible", source);
                    var orgMembers = org.GetMembers();
                    // TODO: Check if it needs to be a DAO member
                    //Runtime.ExpectFiltered(orgMembers.Contains(destination), "destination must be a member of the org", destination);
                    Runtime.ExpectWarning(Runtime.Transaction.Signatures.Length == orgMembers.Length, "must be signed by all org members", source);
                    var msg = Runtime.Transaction.ToByteArray(false);
                    foreach (var signature in Runtime.Transaction.Signatures)
                    {
                        Runtime.ExpectWarning(signature.Verify(msg, orgMembers), "invalid signature", source);
                    }

                    isOrganizationTransaction = true;
                }
                else if (Runtime.ProtocolVersion >= 10)
                {
                    Runtime.ExpectWarning(org != null, "moving funds from orgs currently not possible", source);
                    bool isKnownException = false;
                    if (Runtime.ProtocolVersion >= 12)
                    {
                        if (org.ID == DomainSettings.PhantomForceOrganizationName &&
                            (Runtime.CurrentContext.Name == "stake" || Runtime.CurrentContext.Name == "gas"))
                        {
                            isKnownException = true;
                        }
                    }
                    var orgMembers = org.GetMembers();
                    var numberOfSignaturesNeeded = orgMembers.Length;
                    if (numberOfSignaturesNeeded <= 0)
                    {
                        numberOfSignaturesNeeded = 1;
                    }

                    if (!isKnownException)
                    {
                        Runtime.ExpectWarning(Runtime.Transaction.Signatures.Length >= numberOfSignaturesNeeded,
                            "must be signed by all of the org members", source);
                    }
                    
                    var msg = Runtime.Transaction.ToByteArray(false);
                    var validSignatures = 0;
                    Signature lastSignature = null;
                    var signatures = Runtime.Transaction.Signatures.ToList();

                    foreach (var member in orgMembers)
                    {
                        foreach (var signature in signatures)
                        {
                            if (signature.Verify(msg, member))
                            {
                                validSignatures++;
                                lastSignature = signature;
                                break;
                            }
                        }

                        if (lastSignature != null)
                            signatures.Remove(lastSignature);
                    }

                    if (!isKnownException)
                    {
                        Runtime.ExpectWarning(validSignatures == numberOfSignaturesNeeded,
                            "Number of valid signatures don't match", source);
                    }
                    
                    isOrganizationTransaction = true;
                }
            }
            else
            if (source == DomainSettings.InfusionAddress)
            {
                Runtime.Expect(!_infusionOperationAddress.IsNull, "infusion address is currently locked");
                Runtime.Expect(destination == _infusionOperationAddress, "not valid target for infusion address transfer");
            }
            else if ( Runtime.ProtocolVersion <= 11)
            {
                Runtime.Expect(Runtime.CurrentContext.Name != VirtualMachine.EntryContextName, "moving funds from system address if forbidden");

                var sourceContract = Runtime.Chain.GetContractByAddress(Runtime.Storage, source);
                Runtime.Expect(sourceContract != null, "cannot find matching contract for address: " + source);

                var isKnownExceptionToRule = false;

                if (Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName())
                {
                    if (Nexus.IsNativeContract(sourceContract.Name))
                    {
                        isKnownExceptionToRule = true;
                    }
                    else
                    if (Nexus.TokenExists(Runtime.RootStorage, sourceContract.Name))
                    {
                        isKnownExceptionToRule = true;
                    }
                }

                if (!isKnownExceptionToRule)
                {
                    Runtime.Expect(Runtime.CurrentContext.Name == sourceContract.Name, "moving funds from a contract is forbidden if not made by the contract itself");
                }
            }
            else
            {
                Runtime.Expect(Runtime.CurrentContext.Name != VirtualMachine.EntryContextName, "moving funds from system address if forbidden");
                var isKnownExceptionToRule = false;
                var sourceContract = Runtime.Chain.GetContractByAddress(Runtime.Storage, source);
                Runtime.Expect(sourceContract != null, "cannot find matching contract for address: " + source);
            
                if (Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName() ||
                    Runtime.CurrentContext.Name == NativeContractKind.Gas.GetContractName() )
                {
                    if (Nexus.IsNativeContract(sourceContract.Name))
                    {
                        isKnownExceptionToRule = true;
                    }
                    else
                    if (Nexus.TokenExists(Runtime.RootStorage, sourceContract.Name))
                    {
                        isKnownExceptionToRule = true;
                    }
                }
            
                if (!isKnownExceptionToRule)
                {
                    Runtime.Expect(Runtime.CurrentContext.Name == sourceContract.Name, "moving funds from a contract is forbidden if not made by the contract itself");
                }
            }
        }
        return true;
    }
    
    internal static bool ValidateTransferAmounts(this Nexus Nexus, IRuntime Runtime, Address source, Address destination, IToken token, BigInteger amount, bool isOrganizationTransaction, Address _infusionOperationAddress)
    {
        if (Runtime.HasGenesis)
        {
            var isSystemDestination = destination.IsSystem && NativeContract.GetNativeContractByAddress(destination) != null;
            var isSystemSource = source.IsSystem;
            if (Runtime.ProtocolVersion <= 8)
            {
                if (!isSystemDestination)
                {
                    Runtime.CheckFilterAmountThreshold(token, source, amount, "Transfer Tokens");
                }
            }
            else
            {
                if ( !isSystemSource && !isSystemDestination )
                {
                    Runtime.CheckFilterAmountThreshold(token, source, amount, "Transfer Tokens");
                }
                else if (isSystemSource && !isSystemDestination)
                {
                    if (!isOrganizationTransaction)
                    {
                        if (Runtime.ProtocolVersion <= 9)
                        {
                            Runtime.CheckWarning(Runtime.IsWitness(source), $"Transfer Tokens {amount} {token.Symbol} from {source} to {destination}", source);
                        }
                        else
                        {
                            if (source == DomainSettings.InfusionAddress)
                            {
                                Runtime.CheckWarning(UnitConversion.ToDecimal(amount, token.Decimals) <= Filter.Quota, $"Transfer Tokens {UnitConversion.ToDecimal(amount, token.Decimals)} {token.Symbol} from {source} to {destination}", source);
                            }
                            else
                            {
                                Runtime.CheckWarning(Runtime.IsWitness(source), $"Transfer Tokens {UnitConversion.ToDecimal(amount, token.Decimals)} {token.Symbol} from {source} to {destination}", source);
                            }
                        }
                        //Runtime.CheckFilterAmountThreshold(token, source, amount, "Transfer Tokens");
                    }
                    else
                    {
                        Runtime.ExpectWarning(Runtime.IsWitness(source), $"Transfer Tokens {amount} {token.Symbol} from {source} (System) to {destination}", source);
                    }
                }
                else if (Runtime.ProtocolVersion <= 11 && isSystemDestination)
                {
                    Runtime.ExpectWarning(Runtime.IsWitness(source), $"Transfer Tokens {amount} {token.Symbol} from {source} to {destination}", source);
                }
                else if (Runtime.ProtocolVersion >= 12)
                {
                    if ( isSystemSource && isSystemDestination )
                    {
                        Runtime.CheckWarning(Runtime.IsWitness(source), $"Transfer Tokens {amount} {token.Symbol} from {source} to {destination}", source);
                    }
                }
            }
        }
        return true;
    }
    
    internal static bool ValidateIsTransferAllow(this Nexus Nexus, IRuntime Runtime, Address source, Address destination, IToken token, BigInteger amount, bool isOrganizationTransaction, Address _infusionOperationAddress)
    {
        bool allowed = false;
        if (Runtime.HasGenesis)
        {
            if (Runtime.ProtocolVersion <= 8)
            {
                allowed = Runtime.IsWitness(source);
            }
            else if (isOrganizationTransaction)
            {
                allowed = true;
            }
            else
            {
                if (Runtime.ProtocolVersion <= 11)
                {
                    allowed = Runtime.IsWitness(source);
                }
                else if (Runtime.ProtocolVersion >= 12)
                {
                    var crownToken = TokenUtils.GetContractAddress(DomainSettings.RewardTokenSymbol);
                    var stakeContract = NativeContract.GetAddressForNative(NativeContractKind.Stake);
                    if (isOrganizationTransaction)
                    {
                        allowed = true;
                    }
                    else if ( source == crownToken && destination == stakeContract)
                    {
                        allowed = true;
                    }
                    else
                    {
                        allowed = Runtime.IsWitness(source);
                    }
                }
                else
                {
                    allowed = Runtime.IsWitness(source);
                }
            }
            
        }
        else
        {
            allowed = Runtime.IsPrimaryValidator(source);
        }

#if ALLOWANCE_OPERATIONS
        if (!allowed)
        {
            allowed = Runtime.SubtractAllowance(source, token.Symbol, amount);
        }
#endif

        if (!allowed && source == DomainSettings.InfusionAddress && destination == _infusionOperationAddress)
        {
            allowed = true;
        }
        return allowed;
    }
    
    internal static bool ValidateBasicTransfer(this IRuntime Runtime, Address source, Address destination, string symbol, BigInteger amount)
    {
        Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
        Runtime.Expect(amount > 0, "amount must be positive");
        Runtime.Expect(source != destination, "source and destination must be different");
        Runtime.Expect(!source.IsNull, "invalid source");
        Runtime.Expect(!destination.IsNull, "invalid destination");
        
        var token = Runtime.GetToken(symbol);
        Runtime.Expect(token != null, "invalid token");
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token is not transferable");
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token is not fungible");
        
        return true;
    }

    public static void RegisterValidatorActivity(this INexus Nexus, StorageContext Storage, Address source, Address validatorAddress,
        Timestamp currentTime, Timestamp lastActivity)
    {
        // TODO: Add Validations.
        //Throw.If(source != validatorAddress, "Cannot register activity for yourself");
        
        var governanceValueOfflineTime = Nexus.GetGovernanceValue(Storage, ValidatorContract.ValidatorMaxOfflineTimeTag);
        uint maxPeriod = uint.Parse(governanceValueOfflineTime != 0 ? governanceValueOfflineTime.ToString() : ValidatorContract.ValidatorMaxOfflineTimeDefault.ToString()); // 2 hours
            
        if (lastActivity != Timestamp.Null && lastActivity.Value + maxPeriod <= currentTime)
        {
            DemoteValidator(Nexus, Storage, source, validatorAddress, currentTime, lastActivity);
            return; // validator is offline, do not update activity (Demote)
        }

        var key = SmartContract.GetKeyForField(NativeContractKind.Validator, "_validatorsActivity", true);
        var validatorsActivityMap = new StorageMap(key, Storage);
            
        validatorsActivityMap.Set(validatorAddress, currentTime);
    }
    
    public static void DemoteValidator(this INexus Nexus, StorageContext Storage,  Address source, Address validatorAddress, Timestamp currentTime, Timestamp lastActivity)
    {
        // TODO: Add Validations.
        var key = SmartContract.GetKeyForField(NativeContractKind.Validator, "_validatorsActivity", true);
        var validatorsActivityMap = new StorageMap(key, Storage);

        var count = validatorsActivityMap.Count();
        
        Throw.If(count > 3, "cant remove last validator");
        
        var entry = Nexus.GetValidator(Storage, validatorAddress.TendermintAddress);

        bool brokenRules = false;
        var diff = currentTime - lastActivity;
        var governanceValueOfflineTime = Nexus.GetGovernanceValue(Storage, ValidatorContract.ValidatorMaxOfflineTimeTag);
        var maxPeriod = governanceValueOfflineTime != 0 ? governanceValueOfflineTime : ValidatorContract.ValidatorMaxOfflineTimeDefault; // 2 hours
        if (diff > maxPeriod)
        {
            brokenRules = true;
        }

        var requiredStake = StakeContract.DefaultMasterThreshold;
            
        var stakedAmount = (BigInteger)Nexus.RootChain.InvokeContractAtTimestamp(Storage, currentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), validatorAddress).AsNumber();

        if (stakedAmount < requiredStake)
        {
            brokenRules = true;
        }
        
        Throw.If(brokenRules, "no rules broken");
        
        var index = Nexus.GetIndexOfValidator(validatorAddress, currentTime);

        entry.type = ValidatorType.Invalid;
        var keyValidators = SmartContract.GetKeyForField(NativeContractKind.Validator, "_validators", true);
        var _validatorsMap = new StorageList(keyValidators, Storage);
        
        _validatorsMap.Replace(index, entry);
        
        var Runtime = new RuntimeVM(-1, null, 0, Nexus.RootChain, source, currentTime, Transaction.Null, new StorageChangeSetContext(Storage), null, null);
        Runtime.Notify(EventKind.ValidatorRemove, Runtime.Chain.Address, validatorAddress);
        Runtime.RemoveMember(DomainSettings.ValidatorsOrganizationName, SmartContract.GetAddressForNative(NativeContractKind.Validator), validatorAddress);
    }
}
