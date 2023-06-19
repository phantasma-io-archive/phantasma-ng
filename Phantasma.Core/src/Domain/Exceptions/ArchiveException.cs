using System;

namespace Phantasma.Core.Domain.Exceptions;

public class ArchiveException : Exception
{
    public ArchiveException(string msg) : base(msg)
    {

    }
}
