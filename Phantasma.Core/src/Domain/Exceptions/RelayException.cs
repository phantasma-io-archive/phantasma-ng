using System;

namespace Phantasma.Core.Domain.Exceptions;

public class RelayException : Exception
{
    public RelayException(string msg) : base(msg)
    {

    }
}
