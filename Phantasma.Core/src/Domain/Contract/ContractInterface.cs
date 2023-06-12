using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Triggers;
using Phantasma.Core.Domain.Triggers.Enums;

namespace Phantasma.Core.Domain.Contract;

public sealed class ContractInterface: ISerializable
{
    public static readonly ContractInterface Empty = new ContractInterface(Enumerable.Empty<ContractMethod>(), Enumerable.Empty<ContractEvent>());

    private Dictionary<string, ContractMethod> _methods = new Dictionary<string, ContractMethod>(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<ContractMethod> Methods => _methods.Values;
    public int MethodCount => _methods.Count;

    private ContractEvent[] _events;
    public IEnumerable<ContractEvent> Events => _events;
    public int EventCount => _events.Length;

    public ContractMethod this[string name]
    {
        get
        {
            return FindMethod(name);
        }
    }

    public ContractInterface(IEnumerable<ContractMethod> methods, IEnumerable<ContractEvent> events)
    {
        foreach (var entry in methods)
        {
            _methods[entry.name] = entry;
        }

        this._events = events.ToArray();
    }

    public ContractInterface()
    {
        this._events = new ContractEvent[0];
    }

    public bool HasMethod(string name)
    {
        return _methods.ContainsKey(name);
    }

    public bool HasTokenTrigger(TokenTrigger trigger)
    {
        var strName = trigger.ToString();
        var name = Char.ToLower(strName[0]) + strName.Substring(1);
        return _methods.ContainsKey(name);
    }

    public ContractMethod FindMethod(string name)
    {
        if (_methods.ContainsKey(name))
        {
            return _methods[name];
        }

        return null;
    }

    public ContractEvent FindEvent(byte value)
    {
        foreach (var evt in _events)
        {
            if (evt.value == value)
            {
                return evt;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if this ABI implements a specific event
    /// </summary>
    public bool Implements(ContractEvent evt)
    {
        foreach (var entry in this.Events)
        {
            if (entry.name == evt.name && entry.value == evt.value && entry.returnType == evt.returnType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if this ABI implements a specific method
    /// </summary>
    public bool Implements(ContractMethod method)
    {
        if (!_methods.ContainsKey(method.name))
        {
            return false;
        }

        var thisMethod = _methods[method.name];
        if (thisMethod.parameters.Length != method.parameters.Length)
        {
            return false;
        }

        for (int i = 0; i < method.parameters.Length; i++)
        {
            if (thisMethod.parameters[i].type != method.parameters[i].type)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if this ABI implements of other ABI (eg: other is a subset of this)
    /// </summary>
    public bool Implements(ContractInterface other)
    {
        foreach (var method in other.Methods)
        {
            if (!this.Implements(method))
            {
                return false;
            }
        }

        foreach (var evt in other.Events)
        {
            if (!this.Implements(evt))
            {
                return false;
            }
        }

        return true;
    }

    public void UnserializeData(BinaryReader reader)
    {
        var len = reader.ReadByte();
        _methods.Clear();
        for (int i = 0; i < len; i++)
        {
            var method  = ContractMethod.Unserialize(reader);
            _methods[method.name] = method;
        }

        len = reader.ReadByte();
        this._events = new ContractEvent[len];
        for (int i = 0; i < len; i++)
        {
            _events[i] = ContractEvent.Unserialize(reader);
        }
    }

    public static ContractInterface Unserialize(BinaryReader reader)
    {
        var result = new ContractInterface();
        result.UnserializeData(reader);
        return result;
    }

    public static ContractInterface FromBytes(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        {
            using (var reader = new BinaryReader(stream))
            {
                return Unserialize(reader);
            }
        }
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.Write((byte)_methods.Count);
        foreach (var method in _methods.Values)
        {
            method.Serialize(writer);
        }

        writer.Write((byte)_events.Length);
        foreach (var evt in _events)
        {
            evt.Serialize(writer);
        }
    }

    public byte[] ToByteArray()
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                SerializeData(writer);
            }

            return stream.ToArray();
        }
    }
}
