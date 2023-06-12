using System;

namespace Phantasma.Core.Domain.Exceptions;

public class SwapException : Exception
{
    public SwapException(string msg) : base(msg)
    {

    }
}
