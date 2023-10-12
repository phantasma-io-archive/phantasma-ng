using System;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Exceptions;

public class DuplicatedTransactionException : Exception
{
    public readonly Hash Hash;

    public DuplicatedTransactionException(Hash hash, string msg) : base(msg)
    {
        this.Hash = hash;
    }
}
