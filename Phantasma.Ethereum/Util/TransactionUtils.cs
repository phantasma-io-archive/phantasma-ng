﻿using System.Collections.Generic;
using System.Text;
using System;

namespace Phantasma.Ethereum.Util
{
    public static class TransactionUtils
    {
        public static string CalculateTransactionHash(string rawSignedTransaction)
        {
            var sha3 = new Sha3Keccack();
            return sha3.CalculateHashFromHex(rawSignedTransaction);
        }
    }

    public class UniqueTransactionHashList : HashSet<string>
    {
        public UniqueTransactionHashList() : base(StringComparer.OrdinalIgnoreCase) { }
    }
}