using System;

namespace Phantasma.Infrastructure.API;

public class APIDescriptionAttribute : Attribute
{
    public readonly string Description;

    public APIDescriptionAttribute(string description)
    {
        Description = description;
    }
}