using System;

namespace Phantasma.Core.Domain.Exceptions;

public class BlockGenerationException : Exception
{
    public BlockGenerationException(string msg) : base(msg)
    {

    }
}
