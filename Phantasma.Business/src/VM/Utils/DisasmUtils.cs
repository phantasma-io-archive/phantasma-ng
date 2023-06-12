using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.VM;

namespace Phantasma.Business.VM.Utils
{
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

        private readonly static Dictionary<string, int> _defaultDisasmTable = GetDefaultDisasmTable();


        public static Dictionary<string, int> GetDefaultDisasmTable(uint ProtocolVersion = DomainSettings.LatestKnownProtocol)
        {
            if (_defaultDisasmTable != null)
            {
                return _defaultDisasmTable;
            }

            var table = new Dictionary<string, int>();

            ExtCalls.IterateExtcalls(ProtocolVersion, (methodName, argCount, method) =>
            {
                table[methodName] = argCount;
            });

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

        public static void AddTokenToTable(this Dictionary<string, int> table, IToken token)
        {
            var abi = token.ABI;

            foreach (var method in abi.Methods)
            {
                var key = $"{token.Symbol}.{method.name}";
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

        public static IEnumerable<DisasmMethodCall> ExtractMethodCalls(Disassembler disassembler, uint ProtocolVersion, Dictionary<string, int> methodArgumentCountTable = null, bool detectAndUseJumps = false)
        {
            if (methodArgumentCountTable == null)
            {
                methodArgumentCountTable = GetDefaultDisasmTable(ProtocolVersion);
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

                    case Opcode.JMP:
                    case Opcode.JMPNOT:
                    case Opcode.JMPIF:
                        {
                            if (detectAndUseJumps)
                            {
                                var argIndex = instruction.Opcode == Opcode.JMP ? 0 : 1;
                                var jumpPos = (ushort) instruction.Args[argIndex];

                                if (jumpPos < instruction.Offset)
                                {
                                    throw new ChainException("Loop detected in script: " + instruction);
                                }
                                else
                                {
                                    var found = false;

                                    for (int i = index + 1; i < instructions.Length; i++)
                                    {
                                        if (instructions[i].Offset >= jumpPos)
                                        {
                                            found = true;
                                            index = i;
                                            break;
                                        }
                                    }

                                    if (!found)
                                    {
                                        throw new ChainException("Weird jump detected in script: \" + instruction");
                                    }
                                }
                            }

                            break;
                        }
                }

                index++;
            }

            return result;
        }

        public static IEnumerable<DisasmMethodCall> ExtractMethodCalls(byte[] script, uint ProtocolVersion, Dictionary<string, int> methodArgumentCountTable = null, bool detectAndUseJumps = false)
        {
            var disassembler = new Disassembler(script);
            return ExtractMethodCalls(disassembler, ProtocolVersion, methodArgumentCountTable, detectAndUseJumps);
        }
    }
}
