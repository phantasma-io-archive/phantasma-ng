using System;

namespace Phantasma.Core.Storage.Context;

public class StorageException: Exception
{
    public StorageException(string msg): base(msg)
    {

    }
}
