﻿using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Phantasma.Business.VM;
using Phantasma.Core.Domain;
using Phantasma.Core.Cryptography;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Numerics;
using Phantasma.Core.Utils;

using Xunit;
namespace Phantasma.Business.Tests.VM;

[Collection("TestVMTests")]
public class TestVM : VirtualMachine
{
    private Dictionary<string, Func<ExecutionFrame, ExecutionState>> _interops = new Dictionary<string, Func<ExecutionFrame, ExecutionState>>();
    private Func<string, ExecutionContext> _contextLoader;
    private Dictionary<string, ScriptContext> contexts;

    public enum DebugEnum
    {
        enum1,
        enum2,
        enum3
    }

    public struct DebugStruct
    {
        public int x;
        public int y;
    }

    public class DebugClass
    {
        public int x;

        public DebugClass(int y)
        {
            x = y;
        }
    }

    public TestVM(byte[] script, uint offset) : base(script, offset, null)
    {
        RegisterDefaultInterops();
        RegisterContextLoader(ContextLoader);
        contexts = new Dictionary<string, ScriptContext>();
    }

    private ExecutionContext ContextLoader(string contextName)
    {
        if (contexts.ContainsKey(contextName))
            return contexts[contextName];

        if (contextName == "test")
        {
            var scriptString = new string[]
            {
            $"pop r1",
            $"inc r1",
            $"push r1",
            @"ret",
            };

            var byteScript = BuildScript(scriptString);

            contexts.Add(contextName, new ScriptContext("test", byteScript, 0));

            return contexts[contextName];
        }

        return null;
    }

    public byte[] BuildScript(string[] lines)
    {
        IEnumerable<Semanteme> semantemes = null;
        try
        {
            semantemes = Semanteme.ProcessLines(lines);
        }
        catch (Exception e)
        {
            throw new Exception("Error parsing the script");
        }

        var sb = new ScriptBuilder();
        byte[] script = null;

        try
        {
            foreach (var entry in semantemes)
            {
                Trace.WriteLine($"{entry}");
                entry.Process(sb);
            }
            script = sb.ToScript();
        }
        catch (Exception e)
        {
            throw new Exception("Error assembling the script");
        }

        return script;
    }

    public void RegisterInterop(string method, Func<ExecutionFrame, ExecutionState> callback)
    {
        _interops[method] = callback;
    }

    public void RegisterContextLoader(Func<string, ExecutionContext> callback)
    {
        _contextLoader = callback;
    }

    public override ExecutionState ExecuteInterop(string method)
    {
        if (_interops.ContainsKey(method))
        {
            return _interops[method](this.CurrentFrame);
        }

        throw new NotImplementedException();
    }

    public override ExecutionContext LoadContext(string contextName)
    {
        if (_contextLoader != null)
        {
            return _contextLoader(contextName);
        }

        throw new NotImplementedException();
    }

    public void RegisterDefaultInterops()
    {
        RegisterInterop("Upper", (frame) =>
        {
            var obj = frame.VM.Stack.Pop();
            var str = obj.AsString();
            str = str.ToUpper();
            frame.VM.Stack.Push(VMObject.FromObject(str));
            return ExecutionState.Running;
        });

        RegisterInterop("PushEnum", (frame) =>
        {
            var obj = frame.VM.Stack.Pop();
            var n = obj.AsNumber().ToString();
            DebugEnum enm;

            switch (n)
            {
                case "1":
                    enm = DebugEnum.enum1;
                    break;
                case "2":
                    enm = DebugEnum.enum2;
                    break;
                default:
                    enm = DebugEnum.enum3;
                    break;
            }
            
            frame.VM.Stack.Push(VMObject.FromObject(enm));
            return ExecutionState.Running;
        });

        RegisterInterop("PushDebugClass", (frame) =>
        {
            var obj = frame.VM.Stack.Pop();
            int n = (int)obj.AsNumber();

            DebugClass dbClass = new DebugClass(n);
            
            frame.VM.Stack.Push(VMObject.FromObject(dbClass));
            return ExecutionState.Running;
        });

        RegisterInterop("IncrementDebugClass", (frame) =>
        {
            var obj = frame.VM.Stack.Pop();
            var dbClass = obj.AsInterop<DebugClass>();

            dbClass.x++;

            frame.VM.Stack.Push(VMObject.FromObject(dbClass));
            return ExecutionState.Running;
        });

        RegisterInterop("PushBytes", (frame) =>
        {
            var obj = frame.VM.Stack.Pop();
            string str = obj.AsString();

            var byteArray = Encoding.ASCII.GetBytes(str);

            frame.VM.Stack.Push(VMObject.FromObject(byteArray));
            return ExecutionState.Running;
        });

        RegisterInterop("PushDebugStruct", (frame) =>
        {
            var obj = frame.VM.Stack.Pop();
            int n = (int) obj.AsNumber();

            DebugStruct dbStruct = new DebugStruct();
            dbStruct.x = n;
            dbStruct.y = n;

            frame.VM.Stack.Push(VMObject.FromObject(dbStruct));
            return ExecutionState.Running;
        });

        RegisterInterop("IncrementDebugStruct", (frame) =>
        {
            var obj = frame.VM.Stack.Pop();
            var dbStruct = obj.AsInterop<DebugStruct>();

            dbStruct.x++;

            frame.VM.Stack.Push(VMObject.FromObject(dbStruct));
            return ExecutionState.Running;
        });
    }

    public override void DumpData(List<string> lines)
    {
        // do nothing
    }
}

public class VMTests
{
    [Fact]
    public void Interop()
    {
        var source = PhantasmaKeys.Generate();
        var script = ScriptUtils.BeginScript().CallInterop("Upper", "hello").EndScript();

        var vm = new TestVM(script, 0);
        vm.RegisterDefaultInterops();
        var state = vm.Execute();
        Assert.True(state == ExecutionState.Halt);

        Assert.True(vm.Stack.Count == 1);

        var result = vm.Stack.Pop().AsString();
        Assert.True(result == "HELLO");
    }

    [Fact]
    public void DecodeStruct()
    {
        var bytes = Base16.Decode("010E04076372656174656405C95AC15F040763726561746F720823220100279FB052FA82D619FB33581321E3A5F592507EAC995907B504876ABF6F62421F0409726F79616C746965730302160004046E616D65041C61736461736461617364617364616173646173646161736461736461040B6465736372697074696F6E041C61736461736461617364617364616173646173646161736461736461040474797065030202000408696D61676555524C04096173646173646173640407696E666F55524C0400040E61747472696275746554797065310400040F61747472696275746556616C7565310400040E61747472696275746554797065320400040F61747472696275746556616C7565320400040E61747472696275746554797065330400040F61747472696275746556616C7565330400");

        var obj = VMObject.FromBytes(bytes);

        Assert.True(obj.Type == VMType.Struct);
    }

    [Fact]
    public void ArrayObjects()
    {
        var array = new BigInteger[] { 10, 50, 120, 250 };

        var obj = VMObject.FromArray(array);

        Assert.True(obj.Type == VMType.Struct);

        var temp = obj.ToArray<BigInteger>();

        Assert.True(temp.Length == array.Length);

        for (int i=0; i<array.Length; i++)
        {
            Assert.True(array[i] == temp[i]);
        }
    }

    [Fact]
    public void StringUnicodeArrays()
    {
        var original = "Hello Phantasma";

        var obj = VMObject.FromObject(original);
        Assert.True(obj.Type == VMType.String);

        var temp = VMObject.CastTo(obj, VMType.Struct);
        Assert.True(temp.Type == VMType.Struct);

        var elements = temp.GetChildren();
        Assert.True(elements.Count == original.Length);

        var result = temp.AsString();

        Assert.True(original == result);
    }

    public struct TestStruct : ISerializable
    {
        public string Name;
        public BigInteger Number;

        public TestStruct(string name, BigInteger number)
        {
            Name = name;
            Number = number;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.WriteBigInteger(Number);
            //writer.Close();
        }

        public void UnserializeData(BinaryReader reader)
        {
            Name = reader.ReadString();
            Number = reader.ReadBigInteger();
            //reader.Close();
        }
    }

    [Fact]
    public void EncodeDecodeStruct()
    {
        BigInteger number = 123;
        string name = "my_test";

        var test = new TestStruct(name, number);

        Assert.True(test.Name == name);
        Assert.True(test.Number == number);

        var vmStruct = VMObject.FromStruct(test);

        var backFromStruct = vmStruct.AsStruct<TestStruct>();

        Assert.True(backFromStruct.Name == name);
        Assert.True(backFromStruct.Number == number);

        var vmSerialize = vmStruct.Serialize();
        var backFromSerialize = VMObject.FromBytes(vmSerialize);
        var backToStruct = backFromSerialize.AsStruct<TestStruct>();

        Assert.True(backToStruct.Name == name);
        Assert.True(backToStruct.Number == number);

        test = new TestStruct("", number);
        vmStruct = VMObject.FromStruct(test);
        backFromStruct = vmStruct.AsStruct<TestStruct>();

        Assert.True(string.IsNullOrEmpty(backFromStruct.Name));
        Assert.True(backFromStruct.Number == number);

        vmSerialize = vmStruct.Serialize();
        backFromSerialize = VMObject.FromBytes(vmSerialize);
        backToStruct = backFromSerialize.AsStruct<TestStruct>();

        Assert.True(string.IsNullOrEmpty(backToStruct.Name));
        Assert.True(backToStruct.Number == number);
    }


    public struct TestAddressStruct : ISerializable
    {
        public string Name;
        public Address Owner;

        public TestAddressStruct(string Name, Address Owner)
        {
            this.Name = Name;
            this.Owner = Owner;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Owner.Text);
            //writer.Close();
        }

        public void UnserializeData(BinaryReader reader)
        {
            Name = reader.ReadString();
            Owner = Address.FromText(reader.ReadString());
            //reader.Close();
        }
    }

    [Fact]
    public void EncodeWithAddress()
    {
        Address addr = Address.FromText("P2K6Sm1bUYGsFkxuzHPhia1AbANZaHBJV54RgtQi5q8oK34");
        string name = "my_test";

        TestAddressStruct test = new TestAddressStruct(name, addr);

        Assert.True(test.Name == name);
        Assert.True(test.Owner == addr);

        var vmStruct = VMObject.FromStruct(test);

        var backFromStruct = vmStruct.AsStruct<TestAddressStruct>();

        Assert.True(backFromStruct.Name == name);
        Assert.True(backFromStruct.Owner == addr);

        var vmSerialize = vmStruct.Serialize();
        var backFromSerialize = VMObject.FromBytes(vmSerialize);
        var backToStruct = backFromSerialize.AsStruct<TestAddressStruct>();

        Assert.True(backToStruct.Name == name);
        Assert.True(backToStruct.Owner == addr);
    }


    public struct MyMultiStruct : ISerializable
    {
        public TestAddressStruct One;
        public TestStruct Two;
        public bool Three;

        public MyMultiStruct(TestAddressStruct One, TestStruct Two, bool Three = false)
        {
            this.One = One;
            this.Two = Two;
            this.Three = Three;
        }

        public void SerializeData(BinaryWriter writer)
        {
            One.SerializeData(writer);
            Two.SerializeData(writer);
            writer.Write(Three);
            writer.Close();
        }

        public void UnserializeData(BinaryReader reader)
        {
            One.UnserializeData(reader);
            Two.UnserializeData(reader);
            Three = reader.ReadBoolean();
            reader.Close();
        }
    }

    [Fact]
    public void EncodeDecodeWithMultipleStructures()
    {
        Address addr = Address.FromText("P2K6Sm1bUYGsFkxuzHPhia1AbANZaHBJV54RgtQi5q8oK34");
        string name_2 = "my_test_2";
        TestAddressStruct One = new TestAddressStruct(name_2, addr);

        BigInteger number = 123;
        string name = "my_test";
        TestStruct Two = new TestStruct(name, number);

        bool Three = true;

        MyMultiStruct multi = new MyMultiStruct(One, Two, Three);

        // Test One
        Assert.True(One.Name == name_2);
        Assert.True(One.Owner == addr);

        var vmStruct = VMObject.FromStruct(One);

        var backFromStruct = vmStruct.AsStruct<TestAddressStruct>();

        Assert.True(backFromStruct.Name == name_2);
        Assert.True(backFromStruct.Owner == addr);

        var vmSerialize = vmStruct.Serialize();
        var backFromSerialize = VMObject.FromBytes(vmSerialize);
        var backToStruct = backFromSerialize.AsStruct<TestAddressStruct>();

        Assert.True(backToStruct.Name == name_2);
        Assert.True(backToStruct.Owner == addr);

        // Test Two
        Assert.True(Two.Name == name);
        Assert.True(Two.Number == number);

        var vmStruct_Two = VMObject.FromStruct(Two);

        var backFromStruct_Two = vmStruct_Two.AsStruct<TestStruct>();

        Assert.True(backFromStruct_Two.Name == name);
        Assert.True(backFromStruct_Two.Number == number);

        var vmSerialize_Two = vmStruct_Two.Serialize();
        var backFromSerialize_Two = VMObject.FromBytes(vmSerialize_Two);
        var backToStruct_Two = backFromSerialize_Two.AsStruct<TestStruct>();

        Assert.True(backToStruct_Two.Name == name);
        Assert.True(backToStruct_Two.Number == number);

        // Test Multi
        Assert.True(multi.One.Name == One.Name);
        Assert.True(multi.One.Owner == One.Owner);
        Assert.True(multi.Two.Name == Two.Name);
        Assert.True(multi.Two.Number == Two.Number);
        Assert.True(multi.Three == Three);


        var vmStruct_multi = VMObject.FromStruct(multi);
        var backFromStruct_multi = vmStruct_multi.AsStruct<MyMultiStruct>();

        Assert.True(backFromStruct_multi.One.Name == One.Name);
        Assert.True(backFromStruct_multi.One.Owner == One.Owner);
        Assert.True(backFromStruct_multi.Two.Name == Two.Name);
        Assert.True(backFromStruct_multi.Two.Number == Two.Number);
        Assert.True(backFromStruct_multi.Three == Three);

        var vmSerialize_multi = vmStruct_multi.Serialize();
        var backFromSerialize_multi = VMObject.FromBytes(vmSerialize_multi);
        var backToStruct_multi = backFromSerialize_multi.AsStruct<MyMultiStruct>();

        Assert.True(backToStruct_multi.One.Name == One.Name);
        Assert.True(backToStruct_multi.One.Owner == One.Owner);
        Assert.True(backToStruct_multi.Two.Name == Two.Name);
        Assert.True(backToStruct_multi.Two.Number == Two.Number);
        Assert.True(backToStruct_multi.Three == Three);
    }

    // Test Specific cases
    public enum CharacterState
    {
        None = 0,
        Ingame = 1,
        Battle = 2,
        Team = 3
    }

    public enum ElementType
    {
        Normal = 0,
        Fire = 1,
        Poison = 2,
        Water = 3,
        Grass = 4,
        Steel = 5,
        Eletric = 6,
        Wind = 7
    }

    public struct CharacterROM
    {
        public string Name;
        public BigInteger Seed;
        public BigInteger Health;
        public BigInteger Mana;
        public BigInteger Attack;
        public BigInteger Defense;
        public BigInteger Speed;
        public ElementType Element;

        public CharacterROM(string Name, BigInteger Seed, BigInteger Health, BigInteger Mana, BigInteger Attack, BigInteger Defense, BigInteger Speed, ElementType Element)
        {
            this.Name = Name;
            this.Seed = Seed;
            this.Health = Health;
            this.Mana = Mana;
            this.Attack = Attack;
            this.Defense = Defense;
            this.Speed = Speed;
            this.Element = Element;
        }
    }

    public struct CharacterRAM
    {
        public BigInteger XP;
        public BigInteger Level;
        public CharacterState State;

        public CharacterRAM(BigInteger XP, BigInteger Level, CharacterState State)
        {
            this.XP = XP;
            this.Level = Level;
            this.State = State;
        }
    }
}
