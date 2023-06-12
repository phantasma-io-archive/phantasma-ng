using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Core.Storage.Context;

public class StorageKeyComparer : IEqualityComparer<StorageKey>
{
    public bool Equals(StorageKey left, StorageKey right)
    {
        return left.keyData.SequenceEqual(right.keyData);
    }

    public int GetHashCode(StorageKey obj)
    {
        unchecked
        {
            return obj.keyData.Sum(b => b);
        }
    }
}
