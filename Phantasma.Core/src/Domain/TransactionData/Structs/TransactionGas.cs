using System;
using System.Numerics;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.TransactionData.Structs;

public struct TransactionGas : IComparable<TransactionGas>, IEquatable<TransactionGas>
{
    public static readonly TransactionGas Null = new TransactionGas();
    public Address GasPayer;
    public Address GasTarget;
    public BigInteger GasLimit;
    public BigInteger GasPrice;
        
    #region Operations
    public int CompareTo(TransactionGas other)
    {
        return this.GasPayer == other.GasPayer && this.GasTarget == other.GasTarget && 
               this.GasLimit == other.GasLimit && this.GasPrice == other.GasPrice ? 1 : 0;
    }

    bool IEquatable<TransactionGas>.Equals(TransactionGas other)
    {
        return Equals(other);
    }

    public static bool operator ==(TransactionGas left, TransactionGas right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TransactionGas left, TransactionGas right)
    {
        return !(left == right);
    }


    public static bool operator >(TransactionGas left, TransactionGas right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(TransactionGas left, TransactionGas right)
    {
        return left.CompareTo(right) >= 0;
    }

    public static bool operator <(TransactionGas left, TransactionGas right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(TransactionGas left, TransactionGas right)
    {
        return left.CompareTo(right) <= 0;
    }
    #endregion
}
