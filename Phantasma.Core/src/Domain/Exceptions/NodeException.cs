using System;

namespace Phantasma.Core.Domain.Exceptions;

public class NodeException : Exception
{
    public NodeException(string msg) : base(msg)
    {

    }
}
