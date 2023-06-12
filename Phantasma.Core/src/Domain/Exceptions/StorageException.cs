using System;

namespace Phantasma.Core.Domain.Exceptions;

public class StorageException: Exception
{
    public StorageException(string msg): base(msg)
    {

    }
}
