using System.Text;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.VM;

namespace Phantasma.Business.VM.Utils;

public class DisasmMethodCall
{
    public string ContractName;
    public string MethodName;

    public VMObject[] Arguments;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"{ContractName}.{MethodName}(");
        for (int i=0; i<Arguments.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var arg = Arguments[i];
            sb.Append(arg.ToString());
        }
        sb.Append(")");
        return sb.ToString();
    }
}
