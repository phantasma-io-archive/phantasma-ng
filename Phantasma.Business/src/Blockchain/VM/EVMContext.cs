using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Linq;
using System.Globalization;

using Phantasma.Core;
using Phantasma.Core.Domain;

using Nethereum.ABI;
using Nethereum.Model;
using Nethereum.Signer;
using Phantasma.Core.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Utils;
using Phantasma.Core.Cryptography.Hashing;
using Phantasma.Business.Blockchain.Tokens;
using System.Xml.Linq;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Execution;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.VM;

namespace Phantasma.Business.Blockchain.VM
{

    public static class EVMUtils
    {

        // returns a evm context selector for the address (4byte format)
        public static string ContextSelector(Address address)
        {
            var hash = SHA3Keccak.CalculateHash(address.ToByteArray());
            var encoded = Base16.Encode(hash);
            return encoded.Substring(0, 4);
        }

        // validates that current address is within valid EVM address space
        public static bool IsEVMContext(this Address address)
        {
            if (address.Text.Substring(address.Text.Length - 4).ToUpper() != DomainSettings.FuelTokenSymbol)
            {
                return false;
            }

            return ContextSelector(address) == "04DB";
        }
    }
}
