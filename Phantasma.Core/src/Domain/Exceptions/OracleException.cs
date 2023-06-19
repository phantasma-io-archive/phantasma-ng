using System;

namespace Phantasma.Core.Domain.Exceptions;

public class OracleException : Exception
{
    public OracleException(string msg) : base(msg)
    {

    }
}
