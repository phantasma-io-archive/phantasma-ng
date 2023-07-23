using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract.Exchange;
using Phantasma.Core.Domain.Contract.Exchange.Enums;
using Phantasma.Core.Domain.Contract.Exchange.Structs;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed partial class ExchangeContract : NativeContract
    {
        
        #region OTC TRADES

#pragma warning disable 0649
        internal StorageList _otcBook;
        internal BigInteger _maxOTCOrders = 3;
#pragma warning restore 0649

        /// <summary>
        /// Get all the OTC's orders
        /// </summary>
        /// <returns></returns>
        public ExchangeOrder[] GetOTC()
        {
            return _otcBook.All<ExchangeOrder>();
        }

        /// <summary>
        /// Method used to create OTC orders
        /// </summary>
        /// <param name="from"></param>
        /// <param name="baseSymbol"></param>
        /// <param name="quoteSymbol"></param>
        /// <param name="amount"></param>
        /// <param name="price"></param>
        /// <exception cref="Exception"></exception>
        private void CreateOTC(Address from, string baseSymbol, string quoteSymbol, BigInteger amount, BigInteger price)
        {
            Runtime.Expect(!string.IsNullOrEmpty(baseSymbol), "invalid base symbol");
            Runtime.Expect(!string.IsNullOrEmpty(quoteSymbol), "invalid quoteSymbol symbol");
            Runtime.Expect(amount > 0, "invalid amount");
            Runtime.Expect(price > 0, "invalid price");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var uid = Runtime.GenerateUID();
            var userOrders = 0;
            var count = _otcBook.Count();
            ExchangeOrder lockUpOrder;
            for (int i = 0; i < count; i++)
            {
                lockUpOrder = _otcBook.Get<ExchangeOrder>(i);
                if (lockUpOrder.Creator == from)
                {
                    userOrders++;
                    if (Runtime.ProtocolVersion <= 13)
                    {
                        if (userOrders >= _maxOTCOrders)
                        {
                            throw new Exception("Already have an offer created");
                        }
                    }
                    else
                    {
                        Runtime.Expect(userOrders >= _maxOTCOrders, "Already have an offer created");
                    }
                }
            }

            var baseBalance = Runtime.GetBalance(baseSymbol, from);
            Runtime.Expect(baseBalance >= amount, "invalid seller amount");
            Runtime.TransferTokens(baseSymbol, from, Address, price);

            var order = new ExchangeOrder(uid, Runtime.Time, from, Address, amount, baseSymbol, price, quoteSymbol,
                ExchangeOrderSide.Sell, ExchangeOrderType.OTC);
            _otcBook.Add(order);
        }

        /// <summary>
        /// Method used to accept an OTC order
        /// </summary>
        /// <param name="from">Which address is buying</param>
        /// <param name="uid">Order UID</param>
        public void TakeOrder(Address from, BigInteger uid)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(uid >= 0, "invalid uid");

            var count = _otcBook.Count();
            for (int i = 0; i < count; i++)
            {
                var order = _otcBook.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    var baseBalance = Runtime.GetBalance(order.BaseSymbol, Address);
                    Runtime.Expect(baseBalance >= order.Price, "invalid seller amount");

                    var quoteBalance = Runtime.GetBalance(order.QuoteSymbol, from);
                    Runtime.Expect(quoteBalance >= order.Amount, "invalid buyer amount");

                    Runtime.TransferTokens(order.BaseSymbol, Address, from, order.Price);
                    Runtime.TransferTokens(order.QuoteSymbol, from, order.Creator, order.Amount);
                    _otcBook.RemoveAt(i);
                    return;
                }
            }

            Runtime.Expect(false, "order not found");
        }

        /// <summary>
        /// Method used to cancel an OTC order
        /// </summary>
        /// <param name="from">Which address is buying</param>
        /// <param name="uid">Order UID</param>
        public void CancelOTCOrder(Address from, BigInteger uid)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(uid >= 0, "invalid uid");

            var count = _otcBook.Count();
            ExchangeOrder order;
            for (int i = 0; i < count; i++)
            {
                order = _otcBook.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
                    Runtime.Expect(Runtime.IsWitness(order.Creator), "invalid witness");
                    Runtime.Expect(from == order.Creator, "invalid owner");
                    _otcBook.RemoveAt(i);

                    Runtime.TransferTokens(order.BaseSymbol, Address, order.Creator, order.Price);
                    Runtime.Notify(EventKind.TokenReceive, order.Creator,
                        new TokenEventData(order.BaseSymbol, order.Amount, Runtime.Chain.Name));
                    return;
                }
            }

            // if it reaches here, it means it not found nothing in previous part
            throw new Exception("order not found");
        }

        /*public void SwapTokens(Address buyer, Address seller, string baseSymbol, string quoteSymbol, BigInteger amount, BigInteger price, byte[] signature)
        {
            Runtime.Expect(Runtime.IsWitness(buyer), "invalid witness");
            Runtime.Expect(seller != buyer, "invalid seller");

            Runtime.Expect(seller.IsUser, "seller must be user address");

            Runtime.Expect(Runtime.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var baseBalance = Runtime.GetBalance(baseSymbol, seller);
            Runtime.Expect(baseBalance >= amount, "invalid amount");

            var swap = new TokenSwap()
            {
                baseSymbol = baseSymbol,
                quoteSymbol = quoteSymbol,
                buyer = buyer,
                seller = seller,
                price = price,
                value = amount,
            };

            var msg = Serialization.Serialize(swap);
            Runtime.Expect(Ed25519.Verify(signature, msg, seller.ToByteArray().Skip(2).ToArray()), "invalid signature");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var quoteBalance = Runtime.GetBalance(quoteSymbol, buyer);
            Runtime.Expect(quoteBalance >= price, "invalid balance");

            Runtime.TransferTokens(quoteSymbol, buyer, seller, price);
            Runtime.TransferTokens(baseSymbol, seller, buyer, amount);
        }

        public void SwapToken(Address buyer, Address seller, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, byte[] signature)
        {
            Runtime.Expect(Runtime.IsWitness(buyer), "invalid witness");
            Runtime.Expect(seller != buyer, "invalid seller");

            Runtime.Expect(seller.IsUser, "seller must be user address");

            Runtime.Expect(Runtime.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(baseSymbol);
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var nft = Runtime.ReadToken(baseSymbol, tokenID);
            Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "invalid owner");

            var owner = nft.CurrentOwner;
            Runtime.Expect(owner == seller, "invalid owner");

            var swap = new TokenSwap()
            {
                baseSymbol = baseSymbol,
                quoteSymbol = quoteSymbol,
                buyer = buyer,
                seller = seller,
                price = price,
                value = tokenID,
            };

            var msg = Serialization.Serialize(swap);
            Runtime.Expect(Ed25519.Verify(signature, msg, seller.ToByteArray().Skip(1).ToArray()), "invalid signature");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balance = Runtime.GetBalance(quoteSymbol, buyer);
            Runtime.Expect(balance >= price, "invalid balance");

            Runtime.TransferTokens(quoteSymbol, buyer, owner, price);
            Runtime.TransferToken(baseSymbol, owner, buyer, tokenID);
        }*/

        #endregion
    }
}
