using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Domain;

namespace Phantasma.Business.VM.Utils
{
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

    public static class DisasmUtils
    {
        private static VMObject[] PopArgs(string contract, string method, Stack<VMObject> stack, Dictionary<string, int> methodArgumentCountTable)
        {

            var key = method;
            if (contract != null)
            {
                key = $"{contract}.{method}";
            }

            if (methodArgumentCountTable.ContainsKey(key))
            {
                var argCount = methodArgumentCountTable[key];
                var result = new VMObject[argCount];
                for (int i = 0; i < argCount; i++)
                {
                    if (stack.Count == 0)
                    {
                        throw new System.Exception($"Cannot disassemble method => method {key} expected {argCount} args, {i} were fetched");
                    }

                    result[i] = stack.Pop();
                }
                return result;
            }
            else
            {
                throw new System.Exception("Cannot disassemble method => unknown name: " + key);
            }
        }

        public static Dictionary<string, int> GetDefaultDisasmTable()
        {
            var table = new Dictionary<string, int>();
            table["Runtime.Log"] = 1;
            table["Runtime.Notify"] = 3;
            table["Runtime.IsWitness"] = 1;
            table["Runtime.IsTrigger"] = 0;
            table["Runtime.TransferBalance"] = 3;
            table["Runtime.MintTokens"] = 4;
            table["Runtime.BurnTokens"] = 3;
            table["Runtime.SwapTokens"] = 5;
            table["Runtime.TransferTokens"] = 4;
            table["Runtime.TransferToken"] = 4;
            table["Runtime.MintToken"] = 4;
            table["Runtime.BurnToken"] = 3;
            table["Runtime.InfuseToken"] = 5;
            table["Runtime.DeployContract"] = 4;
            table["Runtime.UpgradeContract"] = 4;

            table["Nexus.CreateToken"] = 3;
            table["Nexus.CreateTokenSeries"] = 7;
            table["Nexus.CreateOrganization"] = 4;
            table["Nexus.BeginInit"] = 1;
            table["Nexus.EndInit"] = 1;
            table["Nexus.GetGovernanceValue"] = 1;

            table["Organization.AddMember"] = 3;

            var nativeContracts = Enum.GetValues<NativeContractKind>();
            foreach (var kind in nativeContracts)
            {
                if (kind == NativeContractKind.Unknown)
                {
                    continue;
                }

                var contract = NativeContract.GetNativeContractByKind(kind);
                table.AddContractToTable(contract);
            }

            // TODO add more here
            return table;
        }

        public static void AddContractToTable(this Dictionary<string, int> table, IContract contract)
        {
            var abi = contract.ABI;

            foreach (var method in abi.Methods)
            {
                var key = $"{contract.Name}.{method.name}";
                table[key] = method.parameters.Length;
            }
        }

        public static IEnumerable<string> ExtractContractNames(Disassembler disassembler)
        {
            var instructions = disassembler.Instructions.ToArray();
            var result = new List<string>();

            int index = 0;
            var regs = new VMObject[16];
            while (index < instructions.Length)
            {
                var instruction = instructions[index];

                switch (instruction.Opcode)
                {
                    case Opcode.LOAD:
                        {
                            var dst = (byte)instruction.Args[0];
                            var type = (VMType)instruction.Args[1];
                            var bytes = (byte[])instruction.Args[2];

                            regs[dst] = new VMObject();
                            regs[dst].SetValue(bytes, type);

                            break;
                        }

                    case Opcode.CTX:
                        {
                            var src = (byte)instruction.Args[0];
                            var dst = (byte)instruction.Args[1];

                            regs[dst] = new VMObject();
                            regs[dst].Copy(regs[src]);
                            break;
                        }

                    case Opcode.SWITCH:
                        {
                            var src = (byte)instruction.Args[0];

                            var contractName = regs[src].AsString();
                            result.Add(contractName);
                            break;
                        }
                }

                index++;
            }

            return result.Distinct();
        }

        public static IEnumerable<string> ExtractContractNames(byte[] script)
        {
            var disassembler = new Disassembler(script);
            return ExtractContractNames(disassembler);
        }

        private readonly static Dictionary<string, int> _defaultDisasmTable = GetDefaultDisasmTable();

        public static IEnumerable<DisasmMethodCall> ExtractMethodCalls(Disassembler disassembler, Dictionary<string, int> methodArgumentCountTable = null)
        {
            if (methodArgumentCountTable == null)
            {
                methodArgumentCountTable = _defaultDisasmTable;
            }

            var instructions = disassembler.Instructions.ToArray();
            var result = new List<DisasmMethodCall>();

            int index = 0;
            var regs = new VMObject[16];
            var stack = new Stack<VMObject>();
            while (index < instructions.Length)
            {
                var instruction = instructions[index];

                switch (instruction.Opcode)
                {
                    case Opcode.LOAD:
                        {
                            var dst = (byte)instruction.Args[0];
                            var type = (VMType)instruction.Args[1];
                            var bytes = (byte[])instruction.Args[2];

                            regs[dst] = new VMObject();
                            regs[dst].SetValue(bytes, type);

                            break;
                        }

                    case Opcode.PUSH:
                        {
                            var src = (byte)instruction.Args[0];
                            var val = regs[src];

                            var temp = new VMObject();
                            temp.Copy(val);
                            stack.Push(temp);
                            break;
                        }

                    case Opcode.CTX:
                        {
                            var src = (byte)instruction.Args[0];
                            var dst = (byte)instruction.Args[1];

                            regs[dst] = new VMObject();
                            regs[dst].Copy(regs[src]);
                            break;
                        }

                    case Opcode.SWITCH:
                        {
                            var src = (byte)instruction.Args[0];
                            var val = regs[src];

                            var contractName = regs[src].AsString();
                            var methodName = stack.Pop().AsString();
                            var args = PopArgs(contractName, methodName, stack, methodArgumentCountTable);
                            result.Add(new DisasmMethodCall() { MethodName = methodName, ContractName = contractName, Arguments = args });
                            break;
                        }

                    case Opcode.EXTCALL:
                        {
                            var src = (byte)instruction.Args[0];
                            var methodName = regs[src].AsString();
                            var args = PopArgs(null, methodName, stack, methodArgumentCountTable);
                            result.Add(new DisasmMethodCall() { MethodName = methodName, ContractName = "", Arguments = args });
                            break;
                        }
                }

                index++;
            }

            return result;
        }

        public static IEnumerable<DisasmMethodCall> ExtractMethodCalls(byte[] script, Dictionary<string, int> methodArgumentCountTable = null)
        {
            var disassembler = new Disassembler(script);
            return ExtractMethodCalls(disassembler, methodArgumentCountTable);
        }
    }
}
