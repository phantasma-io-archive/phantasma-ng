using System;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;

//TODO
namespace Phantasma.Core.Domain.Exceptions
{
    public class InvalidTransactionException : Exception
    {
        public readonly Hash Hash;

        public InvalidTransactionException(Hash hash, string msg) : base(msg)
        {
            this.Hash = hash;
        }
    }
}
