using System;

namespace Phantasma.Core.Domain.Exceptions;

public class ContractException : Exception
{
    public ContractException(string msg) : base(msg)
    {

    }
}
