using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Exchange;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using static Phantasma.Core.Domain.Contract.Exchange.ExchangeOrderSide;
using static Phantasma.Core.Domain.Contract.Exchange.ExchangeOrderType;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed partial class ExchangeContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Exchange;

#pragma warning disable 0649
        internal StorageList _availableBases; // string
        internal StorageList _availableQuotes; // string
        internal StorageMap _orders; //<string, List<Order>>
        internal StorageMap _orderMap; //<uid, string> // maps orders ids to pairs
        internal StorageMap _fills; //<uid, BigInteger>
        internal StorageMap _escrows; //<uid, BigInteger>
        internal StorageList _exchanges;
#pragma warning restore 0649

        public ExchangeContract() : base()
        {
        }

        #region Exchange

        private string BuildOrderKey(ExchangeOrderSide side, string baseSymbol, string quoteSymbol) =>
            $"{side}_{baseSymbol}_{quoteSymbol}";

        public BigInteger GetMinimumQuantity(BigInteger tokenDecimals) => BigInteger.Pow(10, (int)tokenDecimals / 2);
        public BigInteger GetMinimumTokenQuantity(IToken token) => GetMinimumQuantity(token.Decimals);

        public BigInteger GetMinimumSymbolQuantity(string symbol)
        {
            var token = Runtime.GetToken(symbol);
            return GetMinimumQuantity(token.Decimals);
        }

        /// <summary>
        /// Check if the Address is an exchange
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool IsExchange(Address address)
        {
            var count = _exchanges.Count();
            for (int i = 0; i < count; i++)
            {
                var exchange = _exchanges.Get<ExchangeProvider>(i);
                if (exchange.address == address)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Validate the parameters for the exchange
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="totalFee"></param>
        /// <param name="feePercentForExchange"></param>
        /// <param name="feePercentForPool"></param>
        private void ValidateExchangeParameters(string id, string name, BigInteger totalFee,
            BigInteger feePercentForExchange, BigInteger feePercentForPool)
        {
            const int NameMaxLength = 100; // Define a reasonable maximum length for the exchange name

            Runtime.Expect(ValidationUtils.IsValidIdentifier(id), "invalid id");
            Runtime.Expect(!string.IsNullOrEmpty(name) && name.Length <= NameMaxLength,
                $"Name should not be empty and should have a length less than or equal to {NameMaxLength}");

            Runtime.Expect(totalFee >= ExhcangeDexMinimumFee,
                "Total fee should be higher than " + ExhcangeDexMinimumFee);
            Runtime.Expect(feePercentForExchange + feePercentForPool == 100,
                $"Exchange Fee percentage({feePercentForExchange}) + Pool Percentage({feePercentForPool}) should be equal to 100%");
            Runtime.Expect(feePercentForExchange >= 1 && feePercentForExchange <= 99,
                "Fee percentage for exchange should be between 1% and 99%");
            Runtime.Expect(feePercentForPool >= 1 && feePercentForPool <= 99,
                "Fee percentage for pool should be between 1% and 99%");
        }

        /// <summary>
        /// Create a new Exchange
        /// </summary>
        /// <param name="from"></param>
        /// <param name="id"></param>
        /// <param name="name"></param>
        public void CreateExchange(Address from, string id, string name, BigInteger totalFee,
            BigInteger feePercentForExchange, BigInteger feePercentForPool)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            ValidateExchangeParameters(id, name, totalFee, feePercentForExchange, feePercentForPool);

            var exchange = new ExchangeProvider()
            {
                address = from,
                id = id,
                name = name,
                TotalFeePercent = totalFee,
                FeePercentForExchange = feePercentForExchange,
                FeePercentForPool = feePercentForPool
            };

            _exchanges.Add(exchange);
        }

        /// <summary>
        /// Edit an existing exchange
        /// </summary>
        /// <param name="from"></param>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="totalFee"></param>
        /// <param name="feePercentForExchange"></param>
        /// <param name="feePercentForPool"></param>
        public void EditExchange(Address from, string id, string name, BigInteger totalFee,
            BigInteger feePercentForExchange, BigInteger feePercentForPool)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            ValidateExchangeParameters(id, name, totalFee, feePercentForExchange, feePercentForPool);

            var exchange = GetExchange(from);
            var exchangeIndex = GetExchangeID(from);

            Runtime.Expect(exchange.address != ExchangeProvider.Null.address && exchangeIndex >= 0,
                "Exchange not found");
            Runtime.Expect(exchange.address == from, "Sender is not authorized to edit this exchange");

            exchange.name = name;
            exchange.TotalFeePercent = totalFee;
            exchange.FeePercentForExchange = feePercentForExchange;
            exchange.FeePercentForPool = feePercentForPool;

            _exchanges.Replace(exchangeIndex, exchange);
        }

        /// <summary>
        /// Get all the exchanges
        /// </summary>
        /// <returns></returns>
        public ExchangeProvider[] GetExchanges()
        {
            return _exchanges.All<ExchangeProvider>();
        }

        /// <summary>
        /// Get the exchange by Address
        /// </summary>
        /// <returns></returns>
        public ExchangeProvider GetExchange(Address address)
        {
            ExchangeProvider exchange = new ExchangeProvider();
            for (int i = 0; i < _exchanges.Count(); i++)
            {
                exchange = _exchanges.Get<ExchangeProvider>(i);
                if (exchange.address == address)
                {
                    break;
                }
            }

            return exchange;
        }

        /// <summary>
        /// Get the exchange by Address
        /// </summary>
        /// <returns></returns>
        public int GetExchangeID(Address address)
        {
            ExchangeProvider exchange = new ExchangeProvider();
            int id = 0;
            for (int i = 0; i < _exchanges.Count(); i++)
            {
                exchange = _exchanges.Get<ExchangeProvider>(i);
                if (exchange.address == address)
                {
                    id = i;
                    break;
                }
            }

            return id;
        }

        /// <summary>
        /// Open Order
        /// </summary>
        /// <param name="from"></param>
        /// <param name="provider"></param>
        /// <param name="baseSymbol"></param>
        /// <param name="quoteSymbol"></param>
        /// <param name="side"></param>
        /// <param name="orderType"></param>
        /// <param name="orderSize"></param>
        /// <param name="price"></param>
        private void OpenOrder(Address from, Address provider, string baseSymbol, string quoteSymbol,
            ExchangeOrderSide side, ExchangeOrderType orderType, BigInteger orderSize, BigInteger price)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(!string.IsNullOrEmpty(baseSymbol), "invalid base symbol");
            Runtime.Expect(!string.IsNullOrEmpty(quoteSymbol), "invalid quote symbol");

            Runtime.Expect(baseSymbol != quoteSymbol, "invalid base/quote pair");

            Runtime.Expect(Runtime.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(orderSize > 0, "invalid order size");
            Runtime.Expect(price >= 0, "invalid order price");

            if (orderType == OTC)
            {
                Runtime.Expect(side == Sell, "otc order must be sell");
                CreateOTC(from, baseSymbol, quoteSymbol, orderSize, price);
                return;
            }

            //Runtime.Expect(Runtime.GasTarget == provider, "invalid gas target");
            //Runtime.Expect(Runtime.GasTarget == this.Address, "invalid gas target");

            if (orderType != ExchangeOrderType.Market)
            {
                Runtime.Expect(orderSize >= GetMinimumTokenQuantity(baseToken), "order size is not sufficient");
                Runtime.Expect(price >= GetMinimumTokenQuantity(quoteToken), "order price is not sufficient");
            }

            var uid = Runtime.GenerateUID();

            //--------------
            //perform escrow for non-market orders
            string orderEscrowSymbol = CalculateEscrowSymbol(baseToken, quoteToken, side);
            IToken orderEscrowToken = orderEscrowSymbol == baseSymbol ? baseToken : quoteToken;
            BigInteger orderEscrowAmount;
            BigInteger orderEscrowUsage = 0;

            if (orderType == ExchangeOrderType.Market)
            {
                orderEscrowAmount = orderSize;
                Runtime.Expect(orderEscrowAmount >= GetMinimumTokenQuantity(orderEscrowToken),
                    "market order size is not sufficient");
            }
            else
            {
                orderEscrowAmount = CalculateEscrowAmount(orderSize, price, baseToken, quoteToken, side);
            }

            //BigInteger baseTokensUnfilled = orderSize;

            var balance = Runtime.GetBalance(orderEscrowSymbol, from);
            Runtime.Expect(balance >= orderEscrowAmount, "not enough balance");

            Runtime.TransferTokens(orderEscrowSymbol, from, Address, orderEscrowAmount);
            //------------

            var thisOrder = new ExchangeOrder();
            StorageList orderList;
            BigInteger orderIndex = 0;

            thisOrder = new ExchangeOrder(uid, Runtime.Time, from, provider, orderSize, baseSymbol, price, quoteSymbol,
                side, orderType);
            Runtime.Notify(EventKind.OrderCreated, from, uid);

            var key = BuildOrderKey(side, quoteSymbol, baseSymbol);

            orderList = _orders.Get<string, StorageList>(key);
            orderIndex = orderList.Add(thisOrder);
            _orderMap.Set(uid, key);

            var makerSide = side == Buy ? Sell : Buy;
            var makerKey = BuildOrderKey(makerSide, quoteSymbol, baseSymbol);
            var makerOrders = _orders.Get<string, StorageList>(makerKey);

            do
            {
                int bestIndex = -1;
                BigInteger bestPrice = 0;
                Timestamp bestPriceTimestamp = 0;

                ExchangeOrder takerOrder = thisOrder;

                var makerOrdersCount = makerOrders.Count();
                for (int i = 0; i < makerOrdersCount; i++)
                {
                    var makerOrder = makerOrders.Get<ExchangeOrder>(i);

                    if (side == Buy)
                    {
                        if (makerOrder.Price > takerOrder.Price &&
                            orderType != ExchangeOrderType.Market) // too expensive, we wont buy at this price
                        {
                            continue;
                        }

                        if (bestIndex == -1 || makerOrder.Price < bestPrice || makerOrder.Price == bestPrice &&
                            makerOrder.Timestamp < bestPriceTimestamp)
                        {
                            bestIndex = i;
                            bestPrice = makerOrder.Price;
                            bestPriceTimestamp = makerOrder.Timestamp;
                        }
                    }
                    else
                    {
                        if (makerOrder.Price < takerOrder.Price &&
                            orderType != ExchangeOrderType.Market) // too cheap, we wont sell at this price
                        {
                            continue;
                        }

                        if (bestIndex == -1 || makerOrder.Price > bestPrice || makerOrder.Price == bestPrice &&
                            makerOrder.Timestamp < bestPriceTimestamp)
                        {
                            bestIndex = i;
                            bestPrice = makerOrder.Price;
                            bestPriceTimestamp = makerOrder.Timestamp;
                        }
                    }
                }

                if (bestIndex >= 0)
                {
                    //since order "uid" has found a match, the creator of this order will be a taker as he will remove liquidity from the market
                    //and the creator of the "bestIndex" order is the maker as he is providing liquidity to the taker
                    var takerAvailableEscrow = orderEscrowAmount - orderEscrowUsage;
                    var takerEscrowUsage = BigInteger.Zero;
                    var takerEscrowSymbol = orderEscrowSymbol;

                    var makerOrder = makerOrders.Get<ExchangeOrder>(bestIndex);
                    var makerEscrow = _escrows.Get<BigInteger, BigInteger>(makerOrder.Uid);
                    var makerEscrowUsage = BigInteger.Zero;
                    var makerEscrowSymbol = orderEscrowSymbol == baseSymbol ? quoteSymbol : baseSymbol;

                    //Get fulfilled order size in base tokens
                    //and then calculate the corresponding fulfilled order size in quote tokens
                    if (takerEscrowSymbol == baseSymbol)
                    {
                        var makerEscrowBaseEquivalent =
                            Runtime.ConvertQuoteToBase(makerEscrow, makerOrder.Price, baseToken, quoteToken);
                        takerEscrowUsage = takerAvailableEscrow < makerEscrowBaseEquivalent
                            ? takerAvailableEscrow
                            : makerEscrowBaseEquivalent;

                        makerEscrowUsage = CalculateEscrowAmount(takerEscrowUsage, makerOrder.Price, baseToken,
                            quoteToken, Buy);
                    }
                    else
                    {
                        var takerEscrowBaseEquivalent = Runtime.ConvertQuoteToBase(takerAvailableEscrow,
                            makerOrder.Price, baseToken, quoteToken);
                        makerEscrowUsage = makerEscrow < takerEscrowBaseEquivalent
                            ? makerEscrow
                            : takerEscrowBaseEquivalent;

                        takerEscrowUsage = CalculateEscrowAmount(makerEscrowUsage, makerOrder.Price, baseToken,
                            quoteToken, Buy);
                    }

                    Runtime.Expect(takerEscrowUsage <= takerAvailableEscrow,
                        "Taker tried to use more escrow than available");
                    Runtime.Expect(makerEscrowUsage <= makerEscrow, "Maker tried to use more escrow than available");

                    if (takerEscrowUsage < GetMinimumSymbolQuantity(takerEscrowSymbol) ||
                        makerEscrowUsage < GetMinimumSymbolQuantity(makerEscrowSymbol))
                    {
                        break;
                    }

                    Runtime.TransferTokens(takerEscrowSymbol, Address, makerOrder.Creator, takerEscrowUsage);
                    Runtime.TransferTokens(makerEscrowSymbol, Address, takerOrder.Creator, makerEscrowUsage);

                    orderEscrowUsage += takerEscrowUsage;

                    Runtime.Notify(EventKind.OrderFilled, takerOrder.Creator, takerOrder.Uid);
                    Runtime.Notify(EventKind.OrderFilled, makerOrder.Creator, makerOrder.Uid);

                    if (makerEscrowUsage == makerEscrow)
                    {
                        makerOrders.RemoveAt(bestIndex);
                        _orderMap.Remove(makerOrder.Uid);

                        Runtime.Expect(_escrows.ContainsKey(makerOrder.Uid),
                            "An orderbook entry must have registered escrow");
                        _escrows.Remove(makerOrder.Uid);

                        Runtime.Notify(EventKind.OrderClosed, makerOrder.Creator, makerOrder.Uid);
                    }
                    else
                        _escrows.Set(makerOrder.Uid, makerEscrow - makerEscrowUsage);
                }
                else
                    break;
            } while (orderEscrowUsage < orderEscrowAmount);

            var leftoverEscrow = orderEscrowAmount - orderEscrowUsage;

            if (leftoverEscrow == 0 || orderType != Limit)
            {
                orderList.RemoveAt(orderIndex);
                _orderMap.Remove(thisOrder.Uid);
                _escrows.Remove(thisOrder.Uid);

                if (leftoverEscrow > 0)
                {
                    Runtime.TransferTokens(orderEscrowSymbol, Address, thisOrder.Creator, leftoverEscrow);
                    Runtime.Notify(EventKind.OrderCancelled, thisOrder.Creator, thisOrder.Uid);
                }
                else
                    Runtime.Notify(EventKind.OrderClosed, thisOrder.Creator, thisOrder.Uid);
            }
            else
            {
                _escrows.Set(uid, leftoverEscrow);
            }

            //TODO: ADD FEES, SEND THEM TO this.Address FOR NOW
        }

        /// <summary>
        /// Open a market order
        /// </summary>
        /// <param name="from"></param>
        /// <param name="provider"></param>
        /// <param name="baseSymbol"></param>
        /// <param name="quoteSymbol"></param>
        /// <param name="orderSize"></param>
        /// <param name="side"></param>
        public void OpenMarketOrder(Address from, Address provider, string baseSymbol, string quoteSymbol,
            BigInteger orderSize, ExchangeOrderSide side)
        {
            OpenOrder(from, provider, baseSymbol, quoteSymbol, side, ExchangeOrderType.Market, orderSize, 0);
        }

        /// <summary>
        /// Creates a limit order on the exchange
        /// </summary>
        /// <param name="from"></param>
        /// <param name="baseSymbol">For SOUL/KCAL pair, SOUL would be the base symbol</param>
        /// <param name="quoteSymbol">For SOUL/KCAL pair, KCAL would be the quote symbol</param>
        /// <param name="orderSize">Amount of base symbol tokens the user wants to buy/sell</param>
        /// <param name="price">Amount of quote symbol tokens the user wants to pay/receive per unit of base symbol tokens</param>
        /// <param name="side">If the order is a buy or sell order</param>
        /// <param name="IoC">"Immediate or Cancel" flag: if true, requires any unfulfilled parts of the order to be cancelled immediately after a single attempt at fulfilling it.</param>
        public void OpenLimitOrder(Address from, Address provider, string baseSymbol, string quoteSymbol,
            BigInteger orderSize, BigInteger price, ExchangeOrderSide side, bool IoC)
        {
            OpenOrder(from, provider, baseSymbol, quoteSymbol, side, IoC ? ImmediateOrCancel : Limit, orderSize, price);
        }

        /// <summary>
        /// Open an OTC Order
        /// </summary>
        /// <param name="from"></param>
        /// <param name="baseSymbol"></param>
        /// <param name="quoteSymbol"></param>
        /// <param name="ammount"></param>
        /// <param name="price"></param>
        public void OpenOTCOrder(Address from, string baseSymbol, string quoteSymbol, BigInteger ammount,
            BigInteger price)
        {
            OpenOrder(from, Address.Null, baseSymbol, quoteSymbol, Sell, OTC, ammount, price);
        }

        /// <summary>
        /// Cancel's an order
        /// </summary>
        /// <param name="uid"></param>
        /// <exception cref="Exception"></exception>
        public void CancelOrder(BigInteger uid)
        {
            Runtime.Expect(uid >= 0, "Invalid order UID");
            Runtime.Expect(_orderMap.ContainsKey(uid), "order not found");

            var key = _orderMap.Get<BigInteger, string>(uid);
            StorageList orderList = _orders.Get<string, StorageList>(key);

            var count = orderList.Count();
            for (int i = 0; i < count; i++)
            {
                var order = orderList.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    Runtime.Expect(Runtime.IsWitness(order.Creator), "invalid witness");

                    orderList.RemoveAt(i);
                    _orderMap.Remove(uid);
                    _fills.Remove(uid);

                    if (_escrows.ContainsKey(uid))
                    {
                        var leftoverEscrow = _escrows.Get<BigInteger, BigInteger>(uid);
                        if (leftoverEscrow > 0)
                        {
                            var escrowSymbol = order.Side == Sell ? order.QuoteSymbol : order.BaseSymbol;
                            Runtime.TransferTokens(escrowSymbol, Address, order.Creator, leftoverEscrow);
                            Runtime.Notify(EventKind.TokenReceive, order.Creator,
                                new TokenEventData(escrowSymbol, leftoverEscrow, Runtime.Chain.Name));
                        }
                    }

                    return;
                }
            }

            // if it reaches here, it means it not found nothing in previous part
            throw new Exception("order not found");
        }

        /*
        TODO: implement methods that allow cleaning up the order history book.. make sure only the exchange that placed the orders can clear them
        */

        /*
         TODO: implement code for trail stops and a method to allow a 3rd party to update the trail stop, without revealing user or order info
         */

        public BigInteger CalculateEscrowAmount(BigInteger orderSize, BigInteger orderPrice, IToken baseToken,
            IToken quoteToken, ExchangeOrderSide side)
        {
            switch (side)
            {
                case Sell:
                    return orderSize;

                case Buy:
                    return Runtime.ConvertBaseToQuote(orderSize, orderPrice, baseToken, quoteToken);

                default: throw new ContractException("invalid order side");
            }
        }

        public string CalculateEscrowSymbol(IToken baseToken, IToken quoteToken, ExchangeOrderSide side) =>
            side == Sell ? baseToken.Symbol : quoteToken.Symbol;

        /// <summary>
        /// Get Exchange Order by UID
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public ExchangeOrder GetExchangeOrder(BigInteger uid)
        {
            Runtime.Expect(_orderMap.ContainsKey(uid), "order not found");

            var key = _orderMap.Get<BigInteger, string>(uid);
            StorageList orderList = _orders.Get<string, StorageList>(key);

            var count = orderList.Count();
            var order = new ExchangeOrder();
            for (int i = 0; i < count; i++)
            {
                order = orderList.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    //Runtime.Expect(Runtime.IsWitness(order.Creator), "invalid witness");
                    break;
                }
            }

            return order;
        }

        public BigInteger GetOrderLeftoverEscrow(BigInteger uid)
        {
            Runtime.Expect(_escrows.ContainsKey(uid), "order not found");

            return _escrows.Get<BigInteger, BigInteger>(uid);
        }

        public ExchangeOrder[] GetOrderBooks(string baseSymbol, string quoteSymbol)
        {
            return GetOrderBook(baseSymbol, quoteSymbol, false);
        }

        public ExchangeOrder[] GetOrderBook(string baseSymbol, string quoteSymbol, ExchangeOrderSide side)
        {
            return GetOrderBook(baseSymbol, quoteSymbol, true, side);
        }

        private ExchangeOrder[] GetOrderBook(string baseSymbol, string quoteSymbol, bool oneSideFlag,
            ExchangeOrderSide side = Buy)
        {
            var buyKey = BuildOrderKey(Buy, quoteSymbol, baseSymbol);
            var sellKey = BuildOrderKey(Sell, quoteSymbol, baseSymbol);

            var buyOrders = oneSideFlag && side == Buy || !oneSideFlag
                ? _orders.Get<string, StorageList>(buyKey)
                : new StorageList();
            var sellOrders = oneSideFlag && side == Sell || !oneSideFlag
                ? _orders.Get<string, StorageList>(sellKey)
                : new StorageList();

            var buyCount = buyOrders.Context == null ? 0 : buyOrders.Count();
            var sellCount = sellOrders.Context == null ? 0 : sellOrders.Count();

            ExchangeOrder[] orderbook = new ExchangeOrder[(long)(buyCount + sellCount)];

            for (long i = 0; i < buyCount; i++)
            {
                orderbook[i] = buyOrders.Get<ExchangeOrder>(i);
            }

            for (long i = (long)buyCount; i < orderbook.Length; i++)
            {
                orderbook[i] = sellOrders.Get<ExchangeOrder>(i);
            }

            return orderbook;
        }

        #endregion
        
        #region  Migrate
        // TODO: This when back
        public void OnMigrate(Address from, Address to)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(to != Address.Null, "invalid address");
            Runtime.Expect(to != Address, "invalid address");
            //MigrateHolder(from, to);
        }
        #endregion
    }
}
