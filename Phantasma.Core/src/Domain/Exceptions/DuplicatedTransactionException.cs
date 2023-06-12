using System;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Exceptions;

public class DuplicatedTransactionException : Exception
{
    public readonly Hash Hash;

    public DuplicatedTransactionException(Hash hash, string msg) : base(msg)
    {
        this.Hash = hash;
    }
}
