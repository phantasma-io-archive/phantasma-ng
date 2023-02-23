using System.Collections.Generic;
using System.Numerics;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Serilog;

namespace Phantasma.Business.Blockchain;

public static class ChainExtensions
{
    internal static (CodeType, string) ExtractGasInformation(this IChain chain, Transaction tx, out Address from, out Address target, 
        out BigInteger gasPrice, out BigInteger gasLimit, IEnumerable<DisasmMethodCall> methods, Dictionary<string, int> _methodTableForGasExtraction)
    {
        if (!TransactionExtensions.ExtractGasDetailsFromMethods(methods, out from, out target, out gasPrice, out gasLimit, _methodTableForGasExtraction))
        {
            var type = CodeType.NoUserAddress;
            Log.Information("check tx error {type} {Hash}", type, tx.Hash);
            return (type, "AllowGas call not found in transaction script (or wrong number of arguments)");
        }
        return (CodeType.Ok, "");
    }
}
