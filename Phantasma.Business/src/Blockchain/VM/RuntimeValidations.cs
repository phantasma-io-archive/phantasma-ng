using System;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Platform;
using Phantasma.Core.Domain.Token;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM : GasMachine, IRuntime
{
    #region Expect

    private void ExpectAddressSize(Address address, string name, string prefix = "") =>
        Expect(address.GetSize() <= DomainSettings.AddressMaxSize, $"{prefix}{name} exceeds max address size");

    private void ExpectArchiveLength(byte[] content, string name, string prefix = "") =>
        Expect(content.Length <= DomainSettings.ArchiveMaxSize, $"{prefix}{name} exceeds maximum length");

    private void ExpectArgsLength(object[] args, string name, string prefix = "") =>
        ExpectArgsLength(args.Length, name, prefix);

    private void ExpectArgsLength(int length, string name, string prefix = "") =>
        Expect(length <= DomainSettings.ArgsMax, $"{prefix}{name} exceeds max number of arguments");

    private void ExpectHashSize(Hash hash, string name, string prefix = "") =>
        Expect(hash.Size == Hash.Length, $"{prefix}{name} is an incorrect size");

    private void ExpectEnumIsDefined<TEnum>(TEnum value, string name, string prefix = "") where TEnum : struct, Enum =>
        Expect(Enum.IsDefined(value), $"{prefix}{name} is not defined");

    private void ExpectNameLength(string value, string name, string prefix = "") =>
        Expect(value.Length <= DomainSettings.NameMaxLength, $"{prefix}{name} exceeds max length");

    private void ExpectNotNull<T>(T value, string name, string prefix = "") where T : class =>
        Expect(value != null, $"{prefix}{name} should not be null");

    private void ExpectUrlLength(string value, string name, string prefix = "") =>
        Expect(value.Length <= DomainSettings.UrlMaxLength, $"{prefix}{name} exceeds max length");

    private void ExpectRamLength(byte[] ram, string name, string prefix = "") =>
        Expect(ram.Length <= TokenContent.MaxRAMSize,
            $"{prefix}RAM size exceeds maximum allowed, name: {name}, received: {ram.Length}, maximum: {TokenContent.MaxRAMSize}");

    private void ExpectRomLength(byte[] rom, string name, string prefix = "") =>
        Expect(rom.Length <= TokenContent.MaxROMSize,
            $"{prefix}ROM size exceeds maximum allowed, name: {name}, received: {rom.Length}, maximum:{TokenContent.MaxROMSize}");

    private void ExpectScriptLength(byte[] value, string name, string prefix = "") =>
        Expect(value != null ? value.Length <= DomainSettings.ScriptMaxSize : true,
            $"{prefix}{name} exceeds max length");

    private void ExpectTokenExists(string symbol, string prefix = "") =>
        Expect(TokenExists(symbol), $"{prefix}Token does not exist ({symbol})");

    private void ExpectValidToken(IToken token)
    {
        const string prefix = "invalid token: ";
        ExpectNameLength(token.Name, nameof(token.Name), prefix);
        ExpectNameLength(token.Symbol, nameof(token.Symbol), prefix);
        ExpectAddressSize(token.Owner, nameof(token.Owner), prefix);
        ExpectScriptLength(token.Script, nameof(token.Owner), prefix);
        //TODO: Guard against bad ABI?
    }

    private void ExpectValidChainTask(IChainTask task)
    {
        const string prefix = "invalid chain task: ";
        ExpectNotNull(task, nameof(task), prefix);
        ExpectNameLength(task.ContextName, nameof(task.ContextName), prefix);
        ExpectNameLength(task.Method, nameof(task.Method), prefix);
        ExpectAddressSize(task.Owner, nameof(task.Owner), prefix);
        ExpectEnumIsDefined(task.Mode, nameof(task.Mode), prefix);
    }

    private void ExpectValidContractEvent(ContractEvent evt)
    {
        const string prefix = "invalid contract event: ";
        ExpectNameLength(evt.name, nameof(evt.name), prefix);
        ExpectEnumIsDefined(evt.returnType, nameof(evt.returnType), prefix);

        //TODO: Is the max length of the description byte array different than a script byte array?
        ExpectScriptLength(evt.description, nameof(evt.description), prefix);
    }

    private void ExpectValidContractMethod(ContractMethod method)
    {
        const string prefix = "invalid contract method: ";
        ExpectNameLength(method.name, nameof(method.name), prefix);
        ExpectEnumIsDefined(method.returnType, nameof(method.returnType), prefix);
        ExpectArgsLength(method.parameters.Length, nameof(method.parameters));

        foreach (var parameter in method.parameters)
            ExpectValidContractParameter(parameter);
    }

    private void ExpectValidContractInterface(ContractInterface contractInterface)
    {
        ExpectArgsLength(contractInterface.MethodCount, nameof(contractInterface.MethodCount));
        ExpectArgsLength(contractInterface.EventCount, nameof(contractInterface.EventCount));

        foreach (var method in contractInterface.Methods)
            ExpectValidContractMethod(method);
        foreach (var evt in contractInterface.Events)
            ExpectValidContractEvent(evt);
    }

    private void ExpectValidContractParameter(ContractParameter parameter)
    {
        const string prefix = "invalid contract method parameter: ";
        ExpectNameLength(parameter.name, nameof(parameter.name), prefix);
        ExpectEnumIsDefined(parameter.type, nameof(parameter.type), prefix);
    }

    private void ExpectValidPlatformSwapAddress(PlatformSwapAddress swap)
    {
        const string prefix = "invalid platform swap address: ";
        ExpectUrlLength(swap.ExternalAddress, nameof(swap.ExternalAddress), prefix);
        ExpectAddressSize(swap.LocalAddress, nameof(swap.LocalAddress), prefix);
    }

    private void ExpectValidPlatform(IPlatform platform)
    {
        const string prefix = "invalid platform: ";
        ExpectNameLength(platform.Name, nameof(platform.Name), prefix);
        ExpectNameLength(platform.Symbol, nameof(platform.Symbol), prefix);
        ExpectArgsLength(platform.InteropAddresses.Length, nameof(platform.InteropAddresses), prefix);

        foreach (var address in platform.InteropAddresses)
            ExpectValidPlatformSwapAddress(address);
    }

    #endregion
}
