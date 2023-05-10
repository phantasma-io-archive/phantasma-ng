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

namespace Phantasma.Business.Blockchain.VM
{
    public class EVMContext : ExecutionContext
    {
        public readonly static string ContextName = "EVM";

        public string RawTx { get; private set; }
        public override string Name => ContextName;

        private ExecutionState _state;
        public string Error { get; private set; }

        public readonly RuntimeVM Runtime;

        public EVMContext(string rawTx, RuntimeVM runtime)
        {
            _state = ExecutionState.Running;
            Runtime = runtime;
            RawTx = rawTx;
        }

        private void Expect(bool condition, string error)
        {
            if (!condition)
            {
                throw new Exception($"EVM execution failed: {error}");
            }
        }

        public static bool IsValidAddress(string addressText)
        {
            return addressText.StartsWith("0x") && addressText.Length == 42;
        }

        public static Address AddressConvertEthereumToPhantasma(string addressText, INexus nexus)
        {
            Throw.If(!IsValidAddress(addressText), "invalid ethereum address");
            var input = addressText.Substring(2);
            var decodedInput = input.Decode();

            AddressKind kind = AddressKind.User;

            var contract = FindContract(addressText, nexus);
            if (contract != null)
            {
                kind = AddressKind.System;
            }

            var pubKey = new byte[Address.LengthInBytes];
            ByteArrayUtils.CopyBytes(decodedInput, 0, pubKey, 1, decodedInput.Length);
            pubKey[0] = (byte)kind;
            return Address.FromBytes(pubKey);
        }

        public static string AddressConvertPhantasmaToEthereum(Address addr)
        {
            var bytes = addr.ToByteArray().Skip(1).ToArray();
            var encoded = Base16.Encode(bytes);
            return "0x" + encoded;
        }

        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            var transaction = TransactionFactory.CreateTransaction(RawTx) as LegacyTransactionChainId;

            Expect(transaction != null, "Could not unserialize EVM transaction");

            Expect(transaction.VerifyTransaction(), "Invalid EVM signature");

            var sender = transaction.GetSenderAddress();
            var receiver = "0x" + transaction.ReceiveAddress.Encode();

            var nexus = Runtime.Nexus;

            var src = AddressConvertEthereumToPhantasma(sender, nexus);
            var dest = AddressConvertEthereumToPhantasma(receiver, nexus);

            var amount = transaction.Value.AsBigInteger();
            //var amount = BigInteger.Parse(encodedAmount, NumberStyles.HexNumber);
            Expect(amount >= 0, "invalid amount in EVM transaction");

            Runtime.EVM_Block(src, () =>
            {
                // check if we have a transfer
                if (amount > 0)
                {
                    Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, src, dest, amount);
                }

                // check if we have a call to a contract
                // NOTE - both a transfer and call can happen in same transaction
                if (transaction.Data != null && transaction.Data.Length > 0)
                {
                    string error;
                    var result = ETH_CALL(sender, receiver, Base16.Encode(transaction.Data), Runtime.Nexus, out error);
                    Expect(error == null, error);
                }
            });

            return ExecutionState.Halt;
        }

        private static string TokenProperty(INexus nexus, string tokenAddress, out string error, Func<IToken, string> filter)
        {
            var token = FindToken(tokenAddress, nexus);
            if (token != null)
            {
                var abi_encoded = filter(token);

                if (abi_encoded == null)
                {
                    error = "Null token field";
                    return null;
                }

                error = null;
                return abi_encoded;
            }
            else
            {
                error = "Invalid token address";
                return null;
            }
        }

        public static string ETH_CALL(string senderAddress, string targetAddress, string data, INexus nexus, out string error)
        {
            if (string.IsNullOrEmpty(targetAddress))
            {
                error = "Missing target address";
                return null;
            }

            var methodSelector = data.Substring(2, 8);

            if (string.Equals(targetAddress, ExtCallsAddress, StringComparison.OrdinalIgnoreCase))
            {
                return ETH_ExtCall(methodSelector, data, nexus, out error);   
            }

            var contract = FindContract(targetAddress, nexus);
            if (contract != null)
            {
                foreach (var method in contract.ABI.Methods)
                {
                    var thisSelector = method.GetMethodSelector();

                    if (thisSelector == methodSelector)
                    {
                        // TODO extract the args from encoded ethereum ABI
                        //runtime.CallContext(contract.Name, method, args);

                        error = $"Calling Phantasma contracts from within EVM is not implemented yet";
                        return null;
                    }
                }

                error = $"Could not find method with selector {methodSelector} in contract {contract.Name}";
                return null;
            }

            // otherwise, it might be trying to call an Ethereum builtin or an EIP, so we check for that
            // for missing method selectors, search them in this site: https://www.4byte.directory/
            switch (methodSelector)
            {
                case "06fdde03": //name()
                    return TokenProperty(nexus, targetAddress, out error, (token) => EVMUtils.ABIEncodeString(token.Name));

                case "95d89b41": //symbol()
                    return TokenProperty(nexus, targetAddress, out error, (token) => EVMUtils.ABIEncodeString(token.Symbol));

                case "313ce567"://decimals()
                    return TokenProperty(nexus, targetAddress, out error, (token) => EVMUtils.ABIEncodeNumber(token.Decimals));

                case "18160ddd": //totalSupply()
                    return TokenProperty(nexus, targetAddress, out error, (token) => EVMUtils.ABIEncodeNumber(token.MaxSupply));

                case "70a08231": // balanceOf()
                    {
                        var token = FindToken(targetAddress, nexus);

                        if (token != null)
                        {
                            var account = EVMUtils.ABIDecodeAddress(EVMUtils.FetchArgument(data, 0));

                            var addr = EVMContext.AddressConvertEthereumToPhantasma(account, nexus);

                            var balance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, token, addr);

                            var abi_encoded = EVMUtils.ABIEncodeNumber(balance);

                            error = null;
                            return abi_encoded;
                        }
                        else
                        {
                            error = "Invalid token address";
                            return null;
                        }
                    }

                case "c87b56dd": //tokenURI(uint256 _tokenId) => for NTFs
                    {
                        var token = FindToken(targetAddress, nexus);

                        if (token != null)
                        {
                            if (!token.IsFungible())
                            {
                                var tokenID = EVMUtils.ABIDecodeNumber(EVMUtils.FetchArgument(data, 0));

                                TokenContent info = nexus.ReadNFT(nexus.RootStorage, token.Symbol, tokenID);

                                string abi_encoded = null;

                                // this fetches the token info URL for a specific NFT
                                // however... this should return an URL that contains a valid json that follows ERC721 standard
                                // https://eips.ethereum.org/EIPS/eip-721
                                // a solution for this is to return here instead a URL that points to the Phantasma EVMLayer API
                                // and that API will use this code below to try to translate a Phantasma URL standard into ERC712

                                var series = nexus.GetTokenSeries(nexus.RootStorage, token.Symbol, info.SeriesID);
                                if (series != null)
                                {
                                    TokenUtils.FetchProperty(nexus.RootStorage, nexus.RootChain, TokenUtils.InfoURLMethodName, series, tokenID, 
                                        (propName, propValue) =>
                                        {
                                            abi_encoded = EVMUtils.ABIEncodeString(propValue.AsString());
                                        });
                                }

                                error = null;
                                return abi_encoded;
                            }
                            else
                            {
                                error = "Token cannot be fungible";
                                return null;
                            }
                        }
                        else
                        {
                            error = "Invalid token address";
                            return null;
                        }
                    }

                case "a9059cbb": // transfer(address,uint256)
                    {
                        if (string.IsNullOrEmpty(senderAddress))
                        {
                            error = "Missing sender address";
                            return null;
                        }

                        var token = FindToken(targetAddress, nexus);

                        if (token != null)
                        {

                            var receiver = EVMUtils.ABIDecodeAddress(EVMUtils.FetchArgument(data, 0));
                            var amount = EVMUtils.ABIDecodeNumber(EVMUtils.FetchArgument(data, 1));

                            var src = AddressConvertEthereumToPhantasma(senderAddress, nexus);

                            if (src.IsEVMContext())
                            {
                                var dest = AddressConvertEthereumToPhantasma(receiver, nexus);
                                //Runtime.TransferTokens(token.Symbol, src, dest, amount);
                            }

                            error = "Invalid token address";
                            return null;
                        }

                        error = "Invalid token address";
                        return null;
                    }

                // TODO
                //approve(address,uint256) - 095ea7b3
                //allowance(address,address) - dd62ed3e
                // transferFrom(address,address,uint256) - 23b872dd

                // https://eips.ethereum.org/EIPS/eip-165
                case "01ffc9a7":
                    {
                        var targetInterface = data.Substring(10, 8);
                        error = null;

                        switch (targetInterface)
                        {
                            // https://ethereum.org/en/developers/docs/standards/tokens/erc-721/
                            case "80ac58cd":
                                {
                                    var token = FindToken(targetAddress, nexus);
                                    if (token != null)
                                    {
                                        return token.IsFungible() ? "0" : "1"; // returns true if NFT
                                    }

                                    return "0"; // not a token
                                }

                            case "5b5e139f":
                                return "0"; // TODO NFT metadata

                            // https://eips.ethereum.org/EIPS/eip-1155
                            case "d9b67a26":
                                return "0"; // multitoken not suported in Phantasma, always return 0

                            default:
                                error = "Unknown contract interface";
                                return null;
                        }

                        break;
                    }

                default:
                    error = "Unknown ethereum method";
                    return null;
            }
        }

        private static string ETH_ExtCall(string methodSelector, string data, INexus nexus, out string error)
        {
            error = "Extcalls via EVM not implemented yet";
            return null;


            string result = null;
            error = null;

            ExtCalls.IterateExtcalls((methodName, argCount, extCall) =>
            {
                if (result != null)
                {
                    return;
                }

                var thisSelector = EVMUtils.GetMethodSelector(methodName);

                if (thisSelector == methodSelector)
                {
                    //var state = extCall(Runtime);
                    // TODO execute extcall
                    // NOTE the difficult part is extracting the arguments from EVM encoded ABI
                    // after having the decoded args, its just a matter of pushing them into the Runtime.Stack
                }
            });

            if (result == null)
            {
                error = $"Could not find extcall with method selector {methodSelector}";
            }

            return result;
        }

        private static readonly object _lock = new object();


        private static Dictionary<string, string> _tokenAddressMap = null;

        public static IToken FindToken(string ethAddress, INexus nexus)
        {
            lock (_lock)
            {
                if (_tokenAddressMap == null)
                {
                    _tokenAddressMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    var symbols = nexus.GetAvailableTokenSymbols(nexus.RootStorage);

                    foreach (var symbol in symbols)
                    {
                        var addr = ConvertPhantasmaNameToHexAddress(symbol);
                        _tokenAddressMap[addr] = symbol;
                    }
                }

                if (_tokenAddressMap.ContainsKey(ethAddress))
                {
                    var symbol = _tokenAddressMap[ethAddress];
                    return nexus.GetTokenInfo(nexus.RootStorage, symbol);
                }
            }

            return null;
        }

        private static Dictionary<string, string> _contractAddressMap = null;

        public static IContract FindContract(string ethAddress, INexus nexus)
        {
            lock (_lock)
            {
                if (_contractAddressMap == null)
                {
                    _contractAddressMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    var contracts = nexus.RootChain.GetContracts(nexus.RootStorage);

                    foreach (var contract in contracts)
                    {
                        var address = ConvertPhantasmaNameToHexAddress(contract.Name);
                        _contractAddressMap[address] = contract.Name;
                    }
                }

                if (_contractAddressMap.ContainsKey(ethAddress))
                {
                    var name = _contractAddressMap[ethAddress];
                    return nexus.GetContractByName(nexus.RootStorage, name);
                }
            }

            var token = FindToken(ethAddress, nexus);
            if (token != null)
            {
                return nexus.GetContractByName(nexus.RootStorage, token.Symbol);
            }

            return null;
        }

        public static string ConvertPhantasmaNameToHexAddress(string name)
        {
            var input = Encoding.UTF8.GetBytes(name);
            var hash = SHA3Keccak.CalculateHash(input);
            var address = "0x" + Base16.Encode(hash.Skip(12).ToArray());

            Console.WriteLine($"{name}={address}");
            return address; 
        }

        public static readonly string ExtCallsAddress = ConvertPhantasmaNameToHexAddress(".EXTCALLS");
    }

    public static class EVMUtils
    {
        public static string GetMethodSelector(this ContractMethod method)
        {
            return GetMethodSelector(method.name);
        }

        // a method selector in Ethereum is first 4 bytes of the keccak256 of the method name + arguments
        // however in Phantasma we don't allow multiple methods with same name and different arguments
        // due to that + performance reasons, we skip the method arguments here when generating a selector
        public static string GetMethodSelector(string methodName)
        {
            var input = Encoding.UTF8.GetBytes(methodName);
            var hash = SHA3Keccak.CalculateHash(input);

            var output = Base16.Encode(hash);

            return output.Substring(0, 8);
        }

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

        public static string FetchArgument(string data, int idx)
        {
            return data.Substring(10 + idx * 64, 64);
        }

        public static string ABIEncodeNumber(BigInteger val)
        {
            var abiEncode = new ABIEncode();
            return Base16.Encode(abiEncode.GetABIEncoded(val));

            /*
            var encoded = val.ToString("X");
            encoded = ABIPad(encoded);
            return encoded;*/
        }

        public static string ABIEncodeString(string str)
        {
            var abiEncode = new ABIEncode();
            return Base16.Encode(abiEncode.GetABIEncoded(str));

            /*var len = str.Length.ToString("X2");

            var encoded = Base16.Encode(Encoding.UTF8.GetBytes(str));

            encoded = ABIPad(encoded);

            return $"000000000000000000000000000000000000000000000000000000000000002000000000000000000000000000000000000000000000000000000000000000{len}{encoded}";
            */
        }
        public static string ABIDecodeAddress(string encoded)
        {
            return "0x" + encoded.Substring(24);
        }

        public static BigInteger ABIDecodeNumber(string encoded)
        {
            while (encoded.StartsWith('0'))
            {
                encoded = encoded.Substring(1);
            }

            return long.Parse(encoded, NumberStyles.HexNumber);
        }

    }
}
