using System;

namespace Phantasma.Infrastructure.API;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class APIFailCaseAttribute : APIDescriptionAttribute
{
    public readonly string Value;

    public APIFailCaseAttribute(string description, string value) : base(description)
    {
        Value = value;
    }
}