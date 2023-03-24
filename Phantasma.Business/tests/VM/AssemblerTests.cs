using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Numerics;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.RocksDB;
using Shouldly;
using Xunit;
using Phantasma.Business.Blockchain.VM;

namespace Phantasma.Business.Tests.VM;

[Collection(nameof(SystemTestCollectionDefinition))]
public class AssemblerTests
{
    private string PartitionPath { get; set; }
    private INexus Nexus { get; set; }
    private StorageChangeSetContext Context { get; set; }
    private Chain Chain { get; set; }

    [Fact]
    public void Abs()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"-1123124", "1123124"},
            new List<string>() {"0", "0"},
            new List<string>() {"14564535", "14564535" }
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string target = argsLine[1];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                @"abs r1, r2",
                @"push r2",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \"abc\"",
            @"abs r1, r2",
            @"push r2",
            @"ret"
        };
        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Fact]
    public void Move()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<int>>()
        {
            new List<int>() {1, 1},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            object r1 = argsLine[0];
            object target = argsLine[0];    //index 0 is not a typo, we want to copy the reference, not the contents

            scriptString = new string[]
            {
                $"load r1, 1",
                //move it to r2, change its value on the stack and see if it changes on both registers
                @"move r1, r2",
                @"push r2",
                $"push r1",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);
            vm.ExceptionMessage.ShouldBeNull();

            vm.Stack.Count.ShouldBe(2);

            var r1obj = vm.Stack.Pop();
            var r2obj = vm.Stack.Pop();

            r1obj.Type.ShouldBe(VMType.None);
            r2obj.Type.ShouldBe(VMType.Number);
        }
    }

    [Fact]
    public void Copy()
    {
        string[] scriptString;
        RuntimeVM vm;

        scriptString = new string[]
        {
            $"load r1, 12345",
            @"copy r1, r2",
            @"push r2",
            $"push r1",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBeNull();

        var r1obj = vm.Stack.Pop();
        var r2obj = vm.Stack.Pop();

        r1obj.Type.ShouldBe(VMType.Number);
        r2obj.Type.ShouldBe(VMType.Number);
        r1obj.AsNumber().ShouldBe(r2obj.AsNumber());
    }

    [Fact]
    public void Load()
    {
        //TODO: test all VMTypes

        string[] scriptString;
        RuntimeVM vm;

        scriptString = new string[]
        {
            $"load r1, \\\"hello\\\"",
            $"load r2, 123",
            $"load r3, true",
            //load struct
            //load bytes
            //load enum
            //load object

            $"push r3",
            $"push r2",
            $"push r1",
            $"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);

        vm.Stack.Count.ShouldBe(3);

        var str = vm.Stack.Pop().AsString();
        str.CompareTo("hello").ShouldBe(0);

        var num = vm.Stack.Pop().AsNumber();
        num.ShouldBe(new BigInteger(123));

        var bo = vm.Stack.Pop().AsBool();
        bo.ShouldBeTrue();
    }

    [Fact]
    public void Push()
    {
        Load(); //it is effectively the same test
    }

    [Fact]
    public void Pop()
    {
        //TODO: test all VMTypes

        string[] scriptString;
        RuntimeVM vm;

        scriptString = new string[]
        {
            $"load r1, \\\"hello\\\"",
            $"load r2, 123",
            $"load r3, true",
            //load struct
            //load bytes
            //load enum
            //load object

            $"push r3",
            $"push r2",
            $"push r1",

            $"pop r11",
            $"pop r12",
            $"pop r13",

            $"push r13",
            $"push r12",
            $"push r11",
            $"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);

        vm.Stack.Count.ShouldBe(3);

        var str = vm.Stack.Pop().AsString();
        str.CompareTo("hello").ShouldBe(0);

        var num = vm.Stack.Pop().AsNumber();
        num.ShouldBe(new BigInteger(123));

        var bo = vm.Stack.Pop().AsBool();
        bo.ShouldBeTrue();
    }

    [Fact]
    public void Swap()
    {
        string[] scriptString;
        RuntimeVM vm;

        scriptString = new string[]
        {
            $"load r1, \\\"hello\\\"",
            $"load r2, 123",
            $"swap r1, r2",
            $"push r1",
            $"push r2",
            $"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);

        vm.Stack.Count.ShouldBe(2);

        var str = vm.Stack.Pop().AsString();
        str.CompareTo("hello").ShouldBe(0);

        var num = vm.Stack.Pop().AsNumber();
        num.ShouldBe(new BigInteger(123));
    }

    #region FlowOps

    [Fact]
    public void Call()
    {
        var initVal = 2;
        var targetVal = initVal + 1;

        var scriptString = new string[]
        {
            $@"load r1 {initVal}",
            @"push r1",
            @"call @label",
            @"ret",
            $"@label: pop r1",
            @"inc r1",
            $"push r1",
            $"ret"
        };

        var vm = ExecuteScriptIsolated(scriptString);

        vm.Stack.Count.ShouldBe(1);

        var result = vm.Stack.Pop().AsNumber();
        result.ShouldBe(targetVal);
    }

    //[Fact]
    //public void ExtCall()
    //{
    //    string[] scriptString;
    //    RuntimeVM vm;

    //    var args = new List<List<string>>()
    //    {
    //        new List<string>() {"abc", "ABC"},
    //    };

    //    for (int i = 0; i < args.Count; i++)
    //    {
    //        var argsLine = args[i];
    //        var r1 = argsLine[0];
    //        var target = argsLine[1];

    //        scriptString = new string[]
    //        {
    //            $"load r1, \\\"{r1}\\\"",
    //            $"push r1",
    //            $"extcall \\\"Upper\\\"",
    //            @"ret"
    //        };

    //        vm = ExecuteScriptIsolated(scriptString);

    //        vm.Stack.Count.ShouldBe(1);

    //        var result = vm.Stack.Pop().AsString();
    //        result.ShouldBe(target);
    //    }
    //}

    [Fact]
    public void Jmp()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<int>>()
        {
            new List<int>() {1, 1},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            var r1 = argsLine[0];
            var target = argsLine[1];

            scriptString = new string[]
            {
                $"load r1, 1",
                $"jmp @label",
                $"inc r1",
                $"@label: push r1",
                $"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsNumber();
            result.ShouldBe(target);
        }
    }

    [Fact]
    public void JmpConditional()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<int>>()
        {
            new List<int>() {1, 1},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            var r1 = argsLine[0];
            var target = argsLine[1];

            scriptString = new string[]
            {
                $"load r1, true",
                $"load r2, false",
                $"load r3, {r1}",
                $"load r4, {r1}",
                $"jmpif r1, @label",
                $"inc r3",
                $"@label: jmpnot r2, @label2",
                $"inc r4",
                $"@label2: push r3",
                $"push r4",
                $"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(2);

            var result = vm.Stack.Pop().AsNumber();
            result.ShouldBe(target);

            result = vm.Stack.Pop().AsNumber();
            result.ShouldBe(target);
        }
    }

    [Fact]
    public void Throw()
    {
        string[] scriptString;
        RuntimeVM vm = null;

        var args = new List<List<bool>>()
        {
            new List<bool>() {true, true},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            var r1 = argsLine[0];
            var target = argsLine[1];

            scriptString = new string[]
            {
                $"load r1, {r1}",
                $"push r1",
                $"load r1 \"test throw exception\"",
                $"throw r1",
                $"not r1, r1",
                $"pop r2",
                $"push r1",
                $"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);
            vm.ExceptionMessage.ShouldNotBeNull();
            vm.ExceptionMessage.ShouldBe("test throw exception");
        }
    }


    #endregion

    #region LogicalOps
    [Fact]
    public void Not()
    {
        var scriptString = new string[]
        {
            $@"load r1, true",
            @"not r1, r2",
            @"push r2",
            @"ret"
        };

        var vm = ExecuteScriptIsolated(scriptString);

        vm.Stack.Count.ShouldBe(1);

        var result = vm.Stack.Pop().AsString();
        result.ShouldBe("false");

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            @"not r1, r2",
            @"push r2",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Invalid cast: expected bool, got String");
    }

    [Fact]
    public void And()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"true", "true", "true"},
            new List<string>() {"true", "false", "false"},
            new List<string>() {"false", "true", "false"},
            new List<string>() {"false", "false", "false"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"and r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            $@"load r2, false",
            @"and r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("logical op unsupported for type String");
    }

    [Fact]
    public void Or()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"true", "true", "true"},
            new List<string>() {"true", "false", "true"},
            new List<string>() {"false", "true", "true"},
            new List<string>() {"false", "false", "false"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"or r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            $@"load r2, false",
            @"or r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("logical op unsupported for type String");
    }

    [Fact]
    public void Xor()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"true", "true", "false"},
            new List<string>() {"true", "false", "true"},
            new List<string>() {"false", "true", "true"},
            new List<string>() {"false", "false", "false"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"xor r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            $@"load r2, false",
            @"xor r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("logical op unsupported for type String");
    }

    [Fact]
    public void TestEquals()
    {
        string[] scriptString;
        RuntimeVM vm;
        string result;

        var args = new List<List<string>>()
        {
            new List<string>() {"true", "true", "true"},
            new List<string>() {"true", "false", "false"},
            new List<string>() {"1", "1", "true"},
            new List<string>() {"1", "2", "false"},
            new List<string>() { "\\\"hello\\\"", "\\\"hello\\\"", "true"},
            new List<string>() { "\\\"hello\\\"", "\\\"world\\\"", "false"},
            
            //TODO: add lines for bytes, structs, enums and structs
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"equal r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);


            result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }
    }

    [Fact]
    public void LessThan()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"1", "0", "false"},
            new List<string>() {"1", "1", "false"},
            new List<string>() {"1", "2", "true"},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"lt r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            $@"load r2, 2",
            @"lt r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
    }

    [Fact]
    public void GreaterThan()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"1", "0", "true"},
            new List<string>() {"1", "1", "false"},
            new List<string>() {"1", "2", "false"},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"gt r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            $@"load r2, 2",
            @"gt r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
    }

    [Fact]
    public void LesserThanOrEquals()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"1", "0", "false"},
            new List<string>() {"1", "1", "true"},
            new List<string>() {"1", "2", "true"},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"lte r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            $@"load r2, 2",
            @"lte r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
    }

    [Fact]
    public void GreaterThanOrEquals()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"1", "0", "true"},
            new List<string>() {"1", "1", "true"},
            new List<string>() {"1", "2", "false"},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"gte r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            $@"load r2, 2",
            @"gte r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
    }
    #endregion

    #region NumericOps
    [Fact]
    public void Increment()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"1", "2"},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string target = argsLine[1];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                @"inc r1",
                @"push r1",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"hello\\\"",
            @"inc r1",
            @"push r1",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
    }

    [Fact]
    public void Decrement()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"2", "1"},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string target = argsLine[1];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                @"dec r1",
                @"push r1",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"hello\\\"",
            @"dec r1",
            @"push r1",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
    }

    [Fact]
    public void Sign()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"-1123124", "-1"},
            new List<string>() {"0", "0"},
            new List<string>() {"14564535", "1"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string target = argsLine[1];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                @"sign r1, r2",
                @"push r2",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            @"sign r1, r2",
            @"push r2",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
    }

    [Fact]
    public void Negate()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"-1123124", "1123124"},
            new List<string>() {"0", "0"},
            new List<string>() {"14564535", "-14564535" }
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string target = argsLine[1];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                @"negate r1, r2",
                @"push r2",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            @"negate r1, r2",
            @"push r2",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Fact]
    public void Add()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "246196246099661965807160469919750427681847698407517884715668182"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"add r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $@"load r1, true",
            $"load r2, \\\"stuff\\\"",
            @"add r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'stuff' to BigInteger.");
    }

    [Fact]
    public void Sub()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "0"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"sub r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $@"load r1, true",
            $"load r2, \\\"stuff\\\"",
            @"sub r1, r2, r3",
            @"push r3",
            @"ret"
        };


        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'stuff' to BigInteger.");
    }

    [Fact]
    public void Mul()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "15153147898391329927834760664056143940222558862285292671240041298552647375412113910342337827528430805055673715428680681796281"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"mul r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $@"load r1, true",
            $"load r2, \\\"stuff\\\"",
            @"mul r1, r2, r3",
            @"push r3",
            @"ret"
        };


        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'stuff' to BigInteger.");
    }

    [Fact]
    public void Div()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "1"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"div r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $@"load r1, true",
            $"load r2, \\\"stuff\\\"",
            @"div r1, r2, r3",
            @"push r3",
            @"ret"
        };


        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'stuff' to BigInteger.");
    }

    [Fact]
    public void Mod()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "0"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"mod r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $@"load r1, true",
            $"load r2, \\\"stuff\\\"",
            @"mod r1, r2, r3",
            @"push r3",
            @"ret"
        };


        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'stuff' to BigInteger.");
    }

    [Fact]
    public void ShiftLeft()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "100", "156045409571086686325343677668972466714151959338084738385422346983957734263469303184507273216"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"shl r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $@"load r1, true",
            $"load r2, \\\"stuff\\\"",
            @"shl r1, r2, r3",
            @"push r3",
            @"ret"
        };


        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'stuff' to BigInteger.");
    }

    [Fact]
    public void ShiftRight()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "100", "97107296780097167688396095959314" }
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"shr r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $@"load r1, true",
            $"load r2, \\\"stuff\\\"",
            @"shr r1, r2, r3",
            @"push r3",
            @"ret"
        };


        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'stuff' to BigInteger.");
    }


    [Fact]
    public void Min()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"1", "0", "0"},
            new List<string>() {"1", "1", "1"},
            new List<string>() {"1", "2", "1"},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"min r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            $@"load r2, 2",
            @"min r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Fact]
    public void Max()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<string>>()
        {
            new List<string>() {"1", "0", "1"},
            new List<string>() {"1", "1", "1"},
            new List<string>() {"1", "2", "2"},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string r2 = argsLine[1];
            string target = argsLine[2];

            scriptString = new string[]
            {
                $@"load r1, {r1}",
                $@"load r2, {r2}",
                @"max r1, r2, r3",
                @"push r3",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(target);
        }

        scriptString = new string[]
        {
            $"load r1, \\\"abc\\\"",
            $@"load r2, 2",
            @"max r1, r2, r3",
            @"push r3",
            @"ret"
        };

        vm = ExecuteScriptIsolated(scriptString);
        vm.ExceptionMessage.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }
    #endregion

    #region ContextOps

    //[Fact]
    //public void ContextSwitching()
    //{
    //    string[] scriptString;
    //    RuntimeVM vm;

    //    var args = new List<int[]>()
    //    {
    //        new int[] {1, 2},
    //    };

    //    for (int i = 0; i < args.Count; i++)
    //    {
    //        var argsLine = args[i];
    //        var r1 = argsLine[0];
    //        var target = argsLine[1];

    //        scriptString = new string[]
    //        {
    //            $"load r1, \\\"test\\\"",
    //            $"load r3, 1",
    //            $"push r3",
    //            $"ctx r1, r2",
    //            $"switch r2",
    //            $"load r5, 42",
    //            $"push r5",
    //            @"ret",
    //        };

    //        vm = ExecuteScriptIsolated(scriptString);

    //        vm.Stack.Count.ShouldBe(2);

    //        var result = vm.Stack.Pop().AsNumber();
    //        result.ShouldBe(42);

    //        result = vm.Stack.Pop().AsNumber();
    //        result.ShouldBe(2);
    //    }
    //}

    #endregion

    #region Array

    [Fact]
    public void PutGet()
    {
        string[] scriptString;
        RuntimeVM vm;

        var args = new List<List<int>>()
        {
            new List<int>() {1, 1},
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            var r1 = argsLine[0];
            var target = argsLine[1];

            scriptString = new string[]
            {
                $"load r1 {r1}",
                $"load r2 \\\"key\\\"",
                $"put r1 r3 r2",
                $"get r3 r4 r2",
                $"push r4",
                @"ret",
            };

            vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsNumber();
            result.ShouldBe(target);
        }
    }

    private struct TestInteropStruct
    {
        public BigInteger ID;
        public string name;
        public Address address;
    }

    //[Fact]
    //public void StructInterop()
    //{
    //    string[] scriptString;
    //    RuntimeVM vm;

    //    var randomKey = PhantasmaKeys.Generate();

    //    var demoValue = new TestInteropStruct()
    //    {
    //        ID = 1234,
    //        name = "monkey",
    //        address = randomKey.Address
    //    };

    //    var hexStr = Base16.Encode(demoValue.address.ToByteArray());

    //    scriptString = new string[]
    //    {
    //        // first field
    //        $"load r1 \\\"ID\\\"",
    //        $"load r2 {demoValue.ID}",
    //        $"put r2 r3 r1",
    //        $"load r1 \\\"name\\\"",

    //        // second field
    //        $"load r2 \\\"{demoValue.name}\\\"",
    //        $"put r2 r3 r1",
    //        $"load r1 \\\"address\\\"",

    //        // third field
    //        // this one is more complex because it is not a primitive type supported in the VM
    //        $"load r2 0x{hexStr}",
    //        $"push r2",
    //        $"extcall \\\"Address()\\\"",
    //        $"pop r2",
    //        $"put r2 r3 r1",
    //        $"push r3",
    //        @"ret",
    //    };

    //    vm = ExecuteScriptIsolated(scriptString, (_vm) =>
    //    {
    //        // here we register the interop for extcall "Address()"
    //        // this part would not need to be here... 
    //        // however this is normally done in the Chain Runtime, which we don't use for those tests
    //        // suggestion: maybe move some of those interop to the VM core?
    //        _vm.RegisterInterop("Address()", (frame) =>
    //        {
    //            var input = _vm.Stack.Pop().AsType(VMType.Bytes);

    //            try
    //            {
    //                var obj = Address.FromBytes((byte[])input);
    //                var tempObj = new VMObject();
    //                tempObj.SetValue(obj);
    //                _vm.Stack.Push(tempObj);
    //            }
    //            catch
    //            {
    //                return ExecutionState.Fault;
    //            }

    //            return ExecutionState.Running;
    //        });
    //    });

    //    vm.Stack.Count.ShouldBe(1);

    //    var temp = vm.Stack.Pop();
    //    temp.ShouldNotBeNull();

    //    var result = temp.ToStruct<TestInteropStruct>();
    //    demoValue.ID.ShouldBe(result.ID);
    //    demoValue.name.ShouldBe(result.name);
    //    demoValue.address.ShouldBe(result.address);
    //}

    [Fact]
    public void ArrayInterop()
    {
        RuntimeVM vm;

        var demoArray = new BigInteger[] { 1, 42, 1024 };

        var script = new List<string>();

        for (int i=0; i<demoArray.Length; i++)
        {
            script.Add($"load r1 {i}");
            script.Add($"load r2 {demoArray[i]}");
            script.Add($"put r2 r3 r1");
        }
        script.Add("push r3");
        script.Add("ret");

        vm = ExecuteScriptIsolated(script);

        vm.Stack.Count.ShouldBe(1);

        var temp = vm.Stack.Pop();
        temp.ShouldNotBeNull();

        var result = temp.ToArray<BigInteger>();
        result.Length.ShouldBe(demoArray.Length);
    }

    #endregion

    #region Data
    [Fact]
    public void Cat()
    {
        var args = new List<List<string>>()
        {
            new List<string>() {"Hello", null},
            new List<string>() {null, " world"},
            new List<string>() {"", ""},
            new List<string>() {"Hello ", "world"}
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0] == null ? null : $"\"{argsLine[0]}\"";
            //string r2 = argsLine[1] == null ? null : $"\\\"{argsLine[1]}\\\"";
            string r2 = argsLine[1] == null ? null : $"\"{argsLine[1]}\"";

            var scriptString = new string[1];

            switch (i)
            {
                case 0:
                    scriptString = new string[]
                    {
                        $@"load r1, {r1}",
                        @"cat r1, r2, r3",
                        @"push r3",
                        @"ret"
                    };
                    break;
                case 1:
                    scriptString = new string[]
                    {
                        $@"load r2, {r2}",
                        @"cat r1, r2, r3",
                        @"push r3",
                        @"ret"
                    };
                    break;
                case 2:
                    scriptString = new string[]
                    {
                        $@"load r1, {r1}",
                        $@"load r2, {r2}",
                        @"cat r1, r2, r3",
                        @"push r3",
                        @"ret"
                    };
                    break;
                case 3:
                    scriptString = new string[]
                    {
                        $@"load r1, {r1}",
                        $@"load r2, {r2}",
                        @"cat r1, r2, r3",
                        @"push r3",
                        @"ret"
                    };
                    break;
            }

            var vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();
            result.ShouldBe(String.Concat(argsLine[0], argsLine[1]));
        }

        var scriptString2 = new string[]
        {
            $"load r1, \\\"Hello\\\"",
            $@"load r2, 1",
            @"cat r1, r2, r3",
            @"push r3",
            @"ret"
        };

        var vm2 = ExecuteScriptIsolated(scriptString2);
        vm2.ExceptionMessage.ShouldBe("Invalid cast during concat opcode");
    }

    [Fact]
    public void Range()
    {
            //TODO: missing tests with byte data

        string r1 = "Hello funny world";
        int index = 6;
        int len = 5;
        string target = "funny";

        var scriptString = new string[1];

        scriptString = new string[]
        {
            $"load r1, \"{r1}\"",
            $"range r1, r2, {index}, {len}",
            @"push r2",
            @"ret"
        };


        var vm = ExecuteScriptIsolated(scriptString);

        vm.Stack.Count.ShouldBe(1);

        var resultBytes = vm.Stack.Pop().AsByteArray();
        var result = Encoding.UTF8.GetString(resultBytes);

        result.ShouldBe(target);
    }

    [Fact]
    public void Left()
    {
        var args = new List<List<string>>()
        {
            new List<string>() {"Hello world", "5", "Hello"},
            //TODO: missing tests with byte data
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string len = argsLine[1];
            string target = argsLine[2];

            var scriptString = new string[1];

            scriptString = new string[]
            {
                $"load r1, \"{r1}\"",
                $"left r1, r2, {len}",
                @"push r2",
                @"ret"
            };
    

            var vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var resultBytes = vm.Stack.Pop().AsByteArray();
            var result = Encoding.UTF8.GetString(resultBytes);
            
            result.ShouldBe(target);
        }
    }

    [Fact]
    public void Right()
    {
        var args = new List<List<string>>()
        {
            new List<string>() {"Hello world", "5", "world"},
            //TODO: missing tests with byte data
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string len = argsLine[1];
            string target = argsLine[2];

            var scriptString = new string[1];

            scriptString = new string[]
            {
                $"load r1, \"{r1}\"",
                $"right r1, r2, {len}",
                @"push r2",
                @"ret"
            };


            var vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var resultBytes = vm.Stack.Pop().AsByteArray();
            var result = Encoding.UTF8.GetString(resultBytes);

            result.ShouldBe(target);
        }
    }

    [Fact]
    public void Size()
    {
        var args = new List<List<string>>()
        {
            new List<string>() {"Hello world"},
            //TODO: missing tests with byte data
        };

        for (int i = 0; i < args.Count; i++)
        {
            var argsLine = args[i];
            string r1 = argsLine[0];
            string target = Encoding.UTF8.GetBytes(argsLine[0]).Length.ToString();

            var scriptString = new string[1];

            scriptString = new string[]
            {
                $"load r1, \"{r1}\"",
                $"size r1, r2",
                @"push r2",
                @"ret"
            };


            var vm = ExecuteScriptIsolated(scriptString);

            vm.Stack.Count.ShouldBe(1);

            var result = vm.Stack.Pop().AsString();

            result.ShouldBe(target);
        }
    }
    #endregion

    public AssemblerTests()
    {
        this.PartitionPath = Path.Combine(Path.GetTempPath(), "PhantasmaUnitTest", $"{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(this.PartitionPath);

        this.Nexus = new Nexus("unittest", (name) => new MemoryStore());
        var maxSupply = 10000000;

        var storage = (StorageContext)new MemoryStorageContext();
        this.Context = new StorageChangeSetContext(storage);

        this.Chain = new Chain((Nexus)this.Nexus, "main");
    }

    private RuntimeVM CreateRuntime(byte[] script)
    {
        var nexusMoq = new Mock<INexus>();

        nexusMoq.Setup( n => n.GetChainByName(
                    It.IsAny<string>())
                ).Returns(this.Chain);

        this.Chain = new Chain(nexusMoq.Object, "main");

        return new RuntimeVM(
                0,
                script,
                0,
                this.Chain,
                Address.Null,
                Timestamp.Now,
                null,
                this.Context,
                null,
                ChainTask.Null
                );
    }

    private RuntimeVM ExecuteScriptIsolated(IEnumerable<string> scriptString, Action<RuntimeVM> beforeExecute = null)
    {
        var script = AssemblerUtils.BuildScript(scriptString);
        var vm = CreateRuntime(script);

        beforeExecute?.Invoke(vm);

        vm.Execute();

        return vm;
    }
}