using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Phantasma.Business.Blockchain
{
    public static class GasExtensions
    {
        public static bool ExtractGasDetailsFromScript(byte[] script, out Address from, out Address target, out BigInteger gasPrice, out BigInteger gasLimit, Dictionary<string, int> methodArgumentCountTable = null)
        {
            var methods = DisasmUtils.ExtractMethodCalls(script, methodArgumentCountTable);
            return ExtractGasDetailsFromMethods(methods, out from, out target, out gasPrice, out gasLimit, methodArgumentCountTable);
        }

        public static bool ExtractGasDetailsFromMethods(IEnumerable<DisasmMethodCall> methods, out Address from, out Address target, out BigInteger gasPrice, out BigInteger gasLimit, Dictionary<string, int> methodArgumentCountTable = null)
        {
            var allowGas = methods.FirstOrDefault(x => x.ContractName.Equals("gas") && x.MethodName.Equals(nameof(GasContract.AllowGas)));

            if (allowGas == null || allowGas.Arguments.Length != 4)
            {
                from = Address.Null;
                target = Address.Null;
                gasPrice = 0;
                gasLimit = 0;
                return false;
            }

            from = allowGas.Arguments[0].AsAddress();
            target = allowGas.Arguments[1].AsAddress();
            gasPrice = allowGas.Arguments[2].AsNumber();
            gasLimit = allowGas.Arguments[3].AsNumber();
            return true;
        }
    }
}
