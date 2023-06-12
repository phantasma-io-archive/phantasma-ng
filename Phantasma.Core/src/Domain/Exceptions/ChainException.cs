using System;

namespace Phantasma.Core.Domain.Exceptions;

public class ChainException : Exception
{
    public ChainException(string msg) : base(msg)
    {

    }
}
