using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Serilog;
using System.Linq;
using System.Numerics;

namespace Phantasma.Business.Blockchain
{
    public static class GasExtensions
    {
        public static bool ExtractGasDetails(byte[] script, out Address from, out Address target, out BigInteger gasPrice, out BigInteger gasLimit)
        {
            var methods = DisasmUtils.ExtractMethodCalls(script);
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
