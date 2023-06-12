using System;
using System.Collections.Generic;
using System.IO;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract;

public class ContractMethod
{
    public readonly string name;
    public readonly VMType returnType;
    public readonly ContractParameter[] parameters;
    public int offset;

    public ContractMethod(string name, VMType returnType, Dictionary<string, int> labels, params ContractParameter[] parameters) 
    {
        if (!labels.ContainsKey(name))
        {
            throw new Exception("Missing offset in label map for method " + name);
        }

        var offset = labels[name];

        this.name = name;
        this.offset = offset;
        this.returnType = returnType;
        this.parameters = parameters;
    }

    public ContractMethod(string name, VMType returnType, int offset, params ContractParameter[] parameters)
    {
        this.name = name;
        this.offset = offset;
        this.returnType = returnType;
        this.parameters = parameters;
    }

    public bool IsProperty()
    {
        if (name.Length >= 4 && name.StartsWith("get") && char.IsUpper(name[3]))
        {
            return true;
        }

        if (name.Length >= 3 && name.StartsWith("is") && char.IsUpper(name[2]))
        {
            return true;
        }

        return false;
    }

    public bool IsTrigger()
    {
        if (name.Length >= 3 && name.StartsWith("on") && char.IsUpper(name[2]))
        {
            return true;
        }

        return false;
    }

    public override string ToString()
    {
        return $"{name} : {returnType}";
    }

    public static ContractMethod FromBytes(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        {
            using (var reader = new BinaryReader(stream))
            {
                return Unserialize(reader);
            }
        }
    }

    public static ContractMethod Unserialize(BinaryReader reader)
    {
        var name = reader.ReadVarString();
        var returnType = (VMType)reader.ReadByte();
        var offset = reader.ReadInt32();
        var len = reader.ReadByte();
        var parameters = new ContractParameter[len];
        for (int i = 0; i < len; i++)
        {
            var pName = reader.ReadVarString();
            var pVMType = (VMType)reader.ReadByte();
            parameters[i] = new ContractParameter(pName, pVMType);
        }

        return new ContractMethod(name, returnType, offset, parameters);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.WriteVarString(name);
        writer.Write((byte)returnType);
        writer.Write((int)offset);
        writer.Write((byte)parameters.Length);
        foreach (var entry in parameters)
        {
            writer.WriteVarString(entry.name);
            writer.Write((byte)entry.type);
        }
    }

    public byte[] ToArray()
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                Serialize(writer);
                return stream.ToArray();
            }
        }
    }

    /*
        public T Invoke<T>(IContract contract, params object[] args)
        {
            return (T)Invoke(contract, args);
        }

        public object Invoke(IContract contract, params object[] args)
        {
            Throw.IfNull(contract, "null contract");
            Throw.IfNull(args, "null args");
            Throw.If(args.Length != this.parameters.Length, "invalid arg count");

            var type = contract.GetType();
            var method = type.GetMethod(this.name);
            Throw.IfNull(method, "ABI mismatch");

            return method.Invoke(contract, args);
        }*/
}
