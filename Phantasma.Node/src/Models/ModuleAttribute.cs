using System;

namespace Phantasma.Node;

[AttributeUsage(AttributeTargets.Class)]
public class ModuleAttribute : Attribute
{
    public readonly string Name;

    public ModuleAttribute(string name)
    {
        Name = name;
    }
}
