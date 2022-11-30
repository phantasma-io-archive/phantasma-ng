using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using static Phantasma.Business.Blockchain.Contracts.ExchangeOrderSide;
using static Phantasma.Business.Blockchain.Contracts.ExchangeOrderType;

namespace Phantasma.Business.Blockchain.Contracts
{
    public struct LPTokenContentROM: ISerializable
    {
        public string Symbol0;
        public string Symbol1;
        public BigInteger ID;

        public LPTokenContentROM(string Symbol0, string Symbol1, BigInteger ID)
        {
            this.Symbol0 = Symbol0;
            this.Symbol1 = Symbol1;
            this.ID = ID;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Symbol0);
            writer.WriteVarString(Symbol1);
            writer.WriteBigInteger(ID);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol0 = reader.ReadVarString();
            Symbol1 = reader.ReadVarString();
            ID = reader.ReadBigInteger();
        }
    }

    public struct LPTokenContentRAM : ISerializable
    {
        public BigInteger Amount0;
        public BigInteger Amount1;
        public BigInteger Liquidity;
        public BigInteger ClaimedFeesSymbol0;
        public BigInteger ClaimedFeesSymbol1;

        public LPTokenContentRAM(BigInteger Amount0, BigInteger Amount1, BigInteger Liquidity)
        {
            this.Amount0 = Amount0;
            this.Amount1 = Amount1;
            this.Liquidity = Liquidity;
            this.ClaimedFeesSymbol0 = 0;
            this.ClaimedFeesSymbol1 = 0;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteBigInteger(Amount0);
            writer.WriteBigInteger(Amount1);
            writer.WriteBigInteger(Liquidity);
            writer.WriteBigInteger(ClaimedFeesSymbol0);
            writer.WriteBigInteger(ClaimedFeesSymbol1);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Amount0 = reader.ReadBigInteger();
            Amount1 = reader.ReadBigInteger();
            Liquidity = reader.ReadBigInteger();
            ClaimedFeesSymbol0 = reader.ReadBigInteger();
            ClaimedFeesSymbol1 = reader.ReadBigInteger();
        }
    }
    
    public struct TradingVolume: ISerializable
    {
        public string Symbol0;
        public string Symbol1;
        public string Day;
        public BigInteger VolumeSymbol0;
        public BigInteger VolumeSymbol1;

        public TradingVolume(string Symbol0, string Symbol1, string Day, BigInteger VolumeSymbol0, BigInteger VolumeSymbol1)
        {
            this.Symbol0 = Symbol0;
            this.Symbol1 = Symbol1;
            this.Day = Day;
            this.VolumeSymbol0 = VolumeSymbol0;
            this.VolumeSymbol1 = VolumeSymbol1;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Symbol0);
            writer.WriteVarString(Symbol1);
            writer.WriteVarString(Day);
            writer.WriteBigInteger(VolumeSymbol0);
            writer.WriteBigInteger(VolumeSymbol1);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol0 = reader.ReadVarString();
            Symbol1 = reader.ReadVarString();
            Day = reader.ReadVarString();
            VolumeSymbol0 = reader.ReadBigInteger();
            VolumeSymbol1 = reader.ReadBigInteger();
        }
    }
    
     public struct Pool: ISerializable
    {
        public string Symbol0; // Symbol
        public string Symbol1; // Pair
        public string Symbol0Address;
        public string Symbol1Address;
        public BigInteger Amount0;
        public BigInteger Amount1;
        public BigInteger FeeRatio;
        public BigInteger TotalLiquidity;
        public BigInteger FeesForUsersSymbol0;
        public BigInteger FeesForUsersSymbol1;
        public BigInteger FeesForOwnerSymbol0;
        public BigInteger FeesForOwnerSymbol1;


        public Pool(string Symbol0, string Symbol1, string Symbol0Address, string Symbol1Address, BigInteger Amount0, BigInteger Amount1, BigInteger FeeRatio, BigInteger TotalLiquidity)
        {
            this.Symbol0 = Symbol0;
            this.Symbol1 = Symbol1;
            this.Symbol0Address = Symbol0Address;
            this.Symbol1Address = Symbol1Address;
            this.Amount0 = Amount0;
            this.Amount1 = Amount1;
            this.FeeRatio = FeeRatio;
            this.TotalLiquidity = TotalLiquidity;
            this.FeesForUsersSymbol0 = 0;
            this.FeesForUsersSymbol1 = 0;
            this.FeesForOwnerSymbol0 = 0;
            this.FeesForOwnerSymbol1 = 0;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Symbol0);
            writer.WriteVarString(Symbol1);
            writer.WriteVarString(Symbol0Address);
            writer.WriteVarString(Symbol1Address);
            writer.WriteBigInteger(Amount0);
            writer.WriteBigInteger(Amount1);
            writer.WriteBigInteger(FeeRatio);
            writer.WriteBigInteger(TotalLiquidity);
            writer.WriteBigInteger(FeesForUsersSymbol0);
            writer.WriteBigInteger(FeesForUsersSymbol1);
            writer.WriteBigInteger(FeesForOwnerSymbol0);
            writer.WriteBigInteger(FeesForOwnerSymbol1);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol0 = reader.ReadVarString();
            Symbol1 = reader.ReadVarString();
            Symbol0Address = reader.ReadVarString();
            Symbol1Address = reader.ReadVarString();
            Amount0 = reader.ReadBigInteger();
            Amount1 = reader.ReadBigInteger();
            FeeRatio = reader.ReadBigInteger();
            TotalLiquidity = reader.ReadBigInteger();
            FeesForUsersSymbol0 = reader.ReadBigInteger();
            FeesForUsersSymbol1 = reader.ReadBigInteger();
            FeesForOwnerSymbol0 = reader.ReadBigInteger();
            FeesForOwnerSymbol1 = reader.ReadBigInteger();
        }
    }

    public struct LPHolderInfo : ISerializable
    {
        public Address Address;
        public BigInteger UnclaimedSymbol0;
        public BigInteger UnclaimedSymbol1;
        public BigInteger ClaimedSymbol0;
        public BigInteger ClaimedSymbol1;

        public LPHolderInfo(Address address, BigInteger unclaimedSymbol0, BigInteger unclaimedSymbol1,  BigInteger claimedSymbol0, BigInteger claimedSymbol1)
        {
            this.Address = address;
            this.UnclaimedSymbol0 = unclaimedSymbol0;
            this.UnclaimedSymbol1 = unclaimedSymbol1;
            this.ClaimedSymbol0 = claimedSymbol0;
            this.ClaimedSymbol1 = claimedSymbol1;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteAddress(Address);
            writer.WriteBigInteger(UnclaimedSymbol0);
            writer.WriteBigInteger(UnclaimedSymbol1);
            writer.WriteBigInteger(ClaimedSymbol0);
            writer.WriteBigInteger(ClaimedSymbol1);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Address = reader.ReadAddress();
            UnclaimedSymbol0 = reader.ReadBigInteger();
            UnclaimedSymbol1 = reader.ReadBigInteger();
            ClaimedSymbol0 = reader.ReadBigInteger();
            ClaimedSymbol1 = reader.ReadBigInteger();
        }
    }
    public enum ExchangeOrderSide
    {
        Buy,
        Sell
    }

    public enum ExchangeOrderType
    {
        OTC,
        Limit,              //normal limit order
        ImmediateOrCancel,  //special limit order, any unfulfilled part of the order gets cancelled if not immediately fulfilled
        Market,             //normal market order
        //TODO: FillOrKill = 4,         //Either gets 100% fulfillment or it gets cancelled , no partial fulfillments like in IoC order types
    }

    public struct ExchangeOrder
    {
        public readonly BigInteger Uid;
        public readonly Timestamp Timestamp;
        public readonly Address Creator;
        public readonly Address Provider;

        public readonly BigInteger Amount;
        public readonly string BaseSymbol;

        public readonly BigInteger Price;
        public readonly string QuoteSymbol;

        public readonly ExchangeOrderSide Side;
        public readonly ExchangeOrderType Type;

        public ExchangeOrder(BigInteger uid, Timestamp timestamp, Address creator, Address provider, BigInteger amount, string baseSymbol, BigInteger price, string quoteSymbol, ExchangeOrderSide side, ExchangeOrderType type)
        {
            Uid = uid;
            Timestamp = timestamp;
            Creator = creator;
            Provider = provider;

            Amount = amount;
            BaseSymbol = baseSymbol;

            Price = price;
            QuoteSymbol = quoteSymbol;

            Side = side;
            Type = type;
        }

        public ExchangeOrder(ExchangeOrder order, BigInteger newPrice, BigInteger newOrderSize)
        {
            Uid = order.Uid;
            Timestamp = order.Timestamp;
            Creator = order.Creator;
            Provider = order.Provider;

            Amount = newOrderSize;
            BaseSymbol = order.BaseSymbol;

            Price = newOrderSize;
            QuoteSymbol = order.QuoteSymbol;

            Side = order.Side;
            Type = order.Type;

        }
    }

    public struct TokenSwap
    {
        public Address buyer;
        public Address seller;
        public string baseSymbol;
        public string quoteSymbol;
        public BigInteger value;
        public BigInteger price;
    }

    public struct ExchangeProvider
    {
        public Address address;
        public string id;
        public string name;
        public BigInteger TotalFeePercent;
        public BigInteger FeePercentForExchange;
        public BigInteger FeePercentForPool;
        public Hash dapp;
    }

    public sealed class ExchangeContract : NativeContract
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

        private string BuildOrderKey(ExchangeOrderSide side, string baseSymbol, string quoteSymbol) => $"{side}_{baseSymbol}_{quoteSymbol}";

        public BigInteger GetMinimumQuantity(BigInteger tokenDecimals) => BigInteger.Pow(10, ((int)tokenDecimals / 2));
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
            for (int i=0; i<count; i++)
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
        /// Create a new Exchange
        /// </summary>
        /// <param name="from"></param>
        /// <param name="id"></param>
        /// <param name="name"></param>
        public void CreateExchange(Address from, string id, string name, BigInteger totalFee, BigInteger feePercentForExchange, BigInteger feePercentForPool)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(ValidationUtils.IsValidIdentifier(id), "invalid id");

            Runtime.Expect(totalFee >= ExhcangeDexMinimumFee, "Total fee should be higher than " + ExhcangeDexMinimumFee);
            Runtime.Expect(feePercentForExchange + feePercentForPool == 100 , $"Exchange Fee percentage({feePercentForExchange}) + Pool Percentage({feePercentForPool}) should be equal to 100%");
            Runtime.Expect(feePercentForExchange >= 1 && feePercentForExchange <= 99 , "Fee percentage for exchange should be between 1% and 99%");
            Runtime.Expect(feePercentForPool >= 1 && feePercentForPool <= 99 , "Fee percentage for pool should be between 1% and 99%");
            
            var exchange = new ExchangeProvider()
            {
                address = from,
                id = id,
                name = name,
                TotalFeePercent = totalFee,
                FeePercentForExchange = feePercentForExchange,
                FeePercentForPool = feePercentForPool
            };

            _exchanges.Add<ExchangeProvider>(exchange);
        }

        public void EditExchange(Address from, string id, string name, BigInteger totalFee, BigInteger feePercentForExchange, BigInteger feePercentForPool)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(ValidationUtils.IsValidIdentifier(id), "invalid id");
            Runtime.Expect(totalFee >= ExhcangeDexMinimumFee, "Total fee should be higher than " + ExhcangeDexMinimumFee);
            Runtime.Expect(feePercentForExchange + feePercentForPool == 100 , $"Exchange Fee percentage({feePercentForExchange}) + Pool Percentage({feePercentForPool}) should be equal to 100%");
            Runtime.Expect(feePercentForExchange >= 1 && feePercentForExchange <= 99 , "Fee percentage for exchange should be between 1% and 99%");
            Runtime.Expect(feePercentForPool >= 1 && feePercentForPool <= 99 , "Fee percentage for pool should be between 1% and 99%");

            var exchange = GetExchange(from);
            var exchangeIndex = GetExchangeID(from);
            exchange.name = name;
            exchange.TotalFeePercent = totalFee;
            exchange.FeePercentForExchange = feePercentForExchange;
            exchange.FeePercentForPool = feePercentForPool;
            
            _exchanges.Replace<ExchangeProvider>(exchangeIndex, exchange);

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
            for (int i=0; i<_exchanges.Count(); i++)
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
            for (int i=0; i<_exchanges.Count(); i++)
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
        private void OpenOrder(Address from, Address provider, string baseSymbol, string quoteSymbol, ExchangeOrderSide side, ExchangeOrderType orderType, BigInteger orderSize, BigInteger price)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(baseSymbol != quoteSymbol, "invalid base/quote pair");

            Runtime.Expect(Runtime.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            if (orderType == ExchangeOrderType.OTC)
            {
                Runtime.Expect(side == ExchangeOrderSide.Sell, "otc order must be sell");
                CreateOTC(from, baseSymbol, quoteSymbol, orderSize, price);
                return;
            }

            //Runtime.Expect(Runtime.GasTarget == provider, "invalid gas target");
            //Runtime.Expect(Runtime.GasTarget == this.Address, "invalid gas target");

            if (orderType != Market)
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

            if (orderType == Market)
            {
                orderEscrowAmount = orderSize;
                Runtime.Expect(orderEscrowAmount >= GetMinimumTokenQuantity(orderEscrowToken), "market order size is not sufficient");
            }
            else
            {
                orderEscrowAmount = CalculateEscrowAmount(orderSize, price, baseToken, quoteToken, side);
            }

            //BigInteger baseTokensUnfilled = orderSize;

            var balance = Runtime.GetBalance(orderEscrowSymbol, from);
            Runtime.Expect(balance >= orderEscrowAmount, "not enough balance");

            Runtime.TransferTokens(orderEscrowSymbol, from, this.Address, orderEscrowAmount);
            //------------

            var thisOrder = new ExchangeOrder();
            StorageList orderList;
            BigInteger orderIndex = 0;

            thisOrder = new ExchangeOrder(uid, Runtime.Time, from, provider, orderSize, baseSymbol, price, quoteSymbol, side, orderType);
            Runtime.Notify(EventKind.OrderCreated, from, uid);

            var key = BuildOrderKey(side, quoteSymbol, baseSymbol);

            orderList = _orders.Get<string, StorageList>(key);
            orderIndex = orderList.Add<ExchangeOrder>(thisOrder);
            _orderMap.Set<BigInteger, string>(uid, key);

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
                        if (makerOrder.Price > takerOrder.Price && orderType != Market) // too expensive, we wont buy at this price
                        {
                            continue;
                        }

                        if (bestIndex == -1 || makerOrder.Price < bestPrice || (makerOrder.Price == bestPrice && makerOrder.Timestamp < bestPriceTimestamp))
                        {
                            bestIndex = i;
                            bestPrice = makerOrder.Price;
                            bestPriceTimestamp = makerOrder.Timestamp;
                        }
                    }
                    else
                    {
                        if (makerOrder.Price < takerOrder.Price && orderType != Market) // too cheap, we wont sell at this price
                        {
                            continue;
                        }

                        if (bestIndex == -1 || makerOrder.Price > bestPrice || (makerOrder.Price == bestPrice && makerOrder.Timestamp < bestPriceTimestamp))
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
                        var makerEscrowBaseEquivalent = Runtime.ConvertQuoteToBase(makerEscrow, makerOrder.Price, baseToken, quoteToken);
                        takerEscrowUsage = takerAvailableEscrow < makerEscrowBaseEquivalent ? takerAvailableEscrow : makerEscrowBaseEquivalent;

                        makerEscrowUsage = CalculateEscrowAmount(takerEscrowUsage, makerOrder.Price, baseToken, quoteToken, Buy);
                    }
                    else
                    {
                        var takerEscrowBaseEquivalent = Runtime.ConvertQuoteToBase(takerAvailableEscrow, makerOrder.Price, baseToken, quoteToken);
                        makerEscrowUsage = makerEscrow < takerEscrowBaseEquivalent ? makerEscrow : takerEscrowBaseEquivalent;

                        takerEscrowUsage = CalculateEscrowAmount(makerEscrowUsage, makerOrder.Price, baseToken, quoteToken, Buy);
                    }

                    Runtime.Expect(takerEscrowUsage <= takerAvailableEscrow, "Taker tried to use more escrow than available");
                    Runtime.Expect(makerEscrowUsage <= makerEscrow, "Maker tried to use more escrow than available");

                    if (takerEscrowUsage < GetMinimumSymbolQuantity(takerEscrowSymbol) ||
                        makerEscrowUsage < GetMinimumSymbolQuantity(makerEscrowSymbol))
                    {

                        break;
                    }

                    Runtime.TransferTokens(takerEscrowSymbol, this.Address, makerOrder.Creator, takerEscrowUsage);
                    Runtime.TransferTokens(makerEscrowSymbol, this.Address, takerOrder.Creator, makerEscrowUsage);

                    orderEscrowUsage += takerEscrowUsage;

                    Runtime.Notify(EventKind.OrderFilled, takerOrder.Creator, takerOrder.Uid);
                    Runtime.Notify(EventKind.OrderFilled, makerOrder.Creator, makerOrder.Uid);

                    if (makerEscrowUsage == makerEscrow)
                    {
                        makerOrders.RemoveAt(bestIndex);
                        _orderMap.Remove(makerOrder.Uid);

                        Runtime.Expect(_escrows.ContainsKey(makerOrder.Uid), "An orderbook entry must have registered escrow");
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
                    Runtime.TransferTokens(orderEscrowSymbol, this.Address, thisOrder.Creator, leftoverEscrow);
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
        public void OpenMarketOrder(Address from, Address provider, string baseSymbol, string quoteSymbol, BigInteger orderSize, ExchangeOrderSide side)
        {
            OpenOrder(from, provider, baseSymbol, quoteSymbol, side, Market, orderSize, 0);
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
        public void OpenLimitOrder(Address from, Address provider, string baseSymbol, string quoteSymbol, BigInteger orderSize, BigInteger price, ExchangeOrderSide side, bool IoC)
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
        public void OpenOTCOrder(Address from, string baseSymbol, string quoteSymbol, BigInteger ammount, BigInteger price)
        {
            OpenOrder(from, Address.Null, baseSymbol, quoteSymbol, ExchangeOrderSide.Sell, ExchangeOrderType.OTC, ammount, price);
        }

        /// <summary>
        /// Cancel's an order
        /// </summary>
        /// <param name="uid"></param>
        /// <exception cref="Exception"></exception>
        public void CancelOrder(BigInteger uid)
        {
            Runtime.Expect(_orderMap.ContainsKey<BigInteger>(uid), "order not found");
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
                    _orderMap.Remove<BigInteger>(uid);
                    _fills.Remove<BigInteger>(uid);

                    if (_escrows.ContainsKey<BigInteger>(uid))
                    {
                        var leftoverEscrow = _escrows.Get<BigInteger, BigInteger>(uid);
                        if (leftoverEscrow > 0)
                        {
                            var escrowSymbol = order.Side == ExchangeOrderSide.Sell ? order.QuoteSymbol : order.BaseSymbol;
                            Runtime.TransferTokens(escrowSymbol, this.Address, order.Creator, leftoverEscrow);
                            Runtime.Notify(EventKind.TokenReceive, order.Creator, new TokenEventData(escrowSymbol, leftoverEscrow, Runtime.Chain.Name));
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

        public BigInteger CalculateEscrowAmount(BigInteger orderSize, BigInteger orderPrice, IToken baseToken, IToken quoteToken, ExchangeOrderSide side)
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

        public string CalculateEscrowSymbol(IToken baseToken, IToken quoteToken, ExchangeOrderSide side) => side == Sell ? baseToken.Symbol : quoteToken.Symbol;

        /// <summary>
        /// Get Exchange Order by UID
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public ExchangeOrder GetExchangeOrder(BigInteger uid)
        {
            Runtime.Expect(_orderMap.ContainsKey<BigInteger>(uid), "order not found");

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

        private ExchangeOrder[] GetOrderBook(string baseSymbol, string quoteSymbol, bool oneSideFlag, ExchangeOrderSide side = Buy)
        {
            var buyKey = BuildOrderKey(Buy, quoteSymbol, baseSymbol);
            var sellKey = BuildOrderKey(Sell, quoteSymbol, baseSymbol);

            var buyOrders = ((oneSideFlag && side == Buy) || !oneSideFlag) ? _orders.Get<string, StorageList>(buyKey) : new StorageList();
            var sellOrders = ((oneSideFlag && side == Sell) || !oneSideFlag) ? _orders.Get<string, StorageList>(sellKey) : new StorageList();

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
            var uid = Runtime.GenerateUID();
            var userOrders = 0;
            var count = _otcBook.Count();
            ExchangeOrder lockUpOrder;
            for (int i = 0; i < count; i++)
            {
                lockUpOrder = _otcBook.Get<ExchangeOrder>(i);
                if(lockUpOrder.Creator == from)
                {
                    userOrders++;
                    if (userOrders >= _maxOTCOrders)
                    {
                        throw new Exception("Already have an offer created");
                    }
                }
            }

            var baseBalance = Runtime.GetBalance(baseSymbol, from);
            Runtime.Expect(baseBalance >= amount, "invalid seller amount");
            Runtime.TransferTokens(baseSymbol, from, this.Address, price);

            var order = new ExchangeOrder(uid, Runtime.Time, from, this.Address, amount, baseSymbol, price, quoteSymbol, ExchangeOrderSide.Sell, ExchangeOrderType.OTC);
            _otcBook.Add<ExchangeOrder>(order);
        }

        /// <summary>
        /// Method used to accept an OTC order
        /// </summary>
        /// <param name="from">Which address is buying</param>
        /// <param name="uid">Order UID</param>
        public void TakeOrder(Address from, BigInteger uid)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var count = _otcBook.Count();
            for (int i=0; i<count; i++)
            {
                var order = _otcBook.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    var baseBalance = Runtime.GetBalance(order.BaseSymbol, this.Address);
                    Runtime.Expect(baseBalance >= order.Price, "invalid seller amount");

                    var quoteBalance = Runtime.GetBalance(order.QuoteSymbol, from);
                    Runtime.Expect(quoteBalance >= order.Amount, "invalid buyer amount");

                    Runtime.TransferTokens(order.BaseSymbol, this.Address, from, order.Price);
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

                    Runtime.TransferTokens(order.BaseSymbol, this.Address, order.Creator, order.Price);
                    Runtime.Notify(EventKind.TokenReceive, order.Creator, new TokenEventData(order.BaseSymbol, order.Amount, Runtime.Chain.Name));
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
        
        #region Dex
        
        internal BigInteger _DEXversion;
        
        /// <summary>
        /// Check if a Token is supported
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public bool IsSupportedToken(string symbol)
        {
            if (!Runtime.TokenExists(symbol))
            {
                return false;
            }

            if (symbol == DomainSettings.StakingTokenSymbol)
            {
                return true;
            }

            if (symbol == DomainSettings.FuelTokenSymbol)
            {
                return true;
            }

            var info = Runtime.GetToken(symbol);
            return info.IsFungible() && info.Flags.HasFlag(TokenFlags.Transferable);
        }
        
        /// <summary>
        /// To deposit tokens on the contract
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol"></param>
        /// <param name="amount"></param>
        public void DepositTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "address must be user address");

            Runtime.Expect(IsSupportedToken(symbol), "token is unsupported");

            var info = Runtime.GetToken(symbol);
            var unitAmount = UnitConversion.GetUnitValue(info.Decimals);
            Runtime.Expect(amount >= unitAmount, "invalid amount");

            Runtime.TransferTokens(symbol, from, this.Address, amount);
        }

        /// <summary>
        /// Swap tokens Pool version (DEX Version)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        public void SwapTokens(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount > 0, $"invalid amount, need to be higher than 0 | {amount}");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(IsSupportedToken(fromSymbol), "source token is unsupported");

            var fromBalance = Runtime.GetBalance(fromSymbol, from);
            Runtime.Expect(fromBalance > 0, $"not enough {fromSymbol} balance");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(IsSupportedToken(toSymbol), "destination token is unsupported");

            var total = GetRate(fromSymbol, toSymbol, amount);
            Runtime.Expect(total > 0, "amount to swap needs to be larger than zero");
            
            if (!PoolExists(fromSymbol, toSymbol)){
                var rate = GetRate(fromSymbol, "SOUL", amount);
                SwapTokens(from, fromSymbol, "SOUL", amount);
                SwapTokens(from, "SOUL", toSymbol, rate);
                return;
            }

            // Validate Pools
            Runtime.Expect(PoolExists(fromSymbol, toSymbol), $"Pool {fromSymbol}/{toSymbol} doesn't exist.");

            Pool pool = GetPool(fromSymbol, toSymbol);

            BigInteger toPotBalance = 0;
            if (pool.Symbol0 == fromSymbol)
                toPotBalance = pool.Amount1;
            else
                toPotBalance = pool.Amount0;

            if (toPotBalance < total && toSymbol == DomainSettings.FuelTokenSymbol)
            {
                var gasAddress = SmartContract.GetAddressForNative(NativeContractKind.Gas);
                var gasBalance = Runtime.GetBalance(toSymbol, gasAddress);
                if (gasBalance >= total)
                {
                    Runtime.TransferTokens(toSymbol, gasAddress, this.Address, total);
                    toPotBalance = total;
                }
            }

            var toSymbolDecimalsInfo = Runtime.GetToken(toSymbol);
            var toSymbolDecimals = Math.Pow(10, toSymbolDecimalsInfo.Decimals);
            var fromSymbolDecimalsInfo = Runtime.GetToken(fromSymbol);
            var fromSymbolDecimals = Math.Pow(10, fromSymbolDecimalsInfo.Decimals);
            Runtime.Expect(toPotBalance >= total, $"insufficient balance in pot, have {(double)toPotBalance / toSymbolDecimals} {toSymbol} in pot, need {(double)total / toSymbolDecimals} {toSymbol}, have {(double)fromBalance / fromSymbolDecimals} {fromSymbol} to convert from");

            bool canBeTraded = false;
            if (pool.Symbol0 == fromSymbol)
                canBeTraded = ValidateTrade(amount, total, pool, true);
            else
                canBeTraded = ValidateTrade(total, amount, pool);

            Runtime.Expect(canBeTraded, $"The trade is not valid.");

            Runtime.TransferTokens(fromSymbol, from, this.Address, amount);
            Runtime.TransferTokens(toSymbol, this.Address, from, total);

            // Trading volume
            UpdateTradingVolume(pool, fromSymbol, amount);
            
            // Handle Fees
            // Fees are always based on SOUL traded.
            BigInteger totalFees = 0;
            BigInteger feeForUsers = 0;
            BigInteger feeForOwner = 0;
            // total *  ExhcangeDexDefaultFee / 100; To calculate the total fees in the pool
            totalFees = total * ExhcangeDexDefaultFee/100;
            feeForUsers = totalFees * 100 / ExchangeDexDefaultPoolPercent;
            feeForOwner = totalFees * 100 / ExchangeDexDefaultGovernancePercent;
            
            // Total * 100 - 3 (ExchangeDexDefaultFee) / 100 - This is used to remove the fees amount that goes into the pool
            var totalAmountToRemove = total * (100 - ExhcangeDexDefaultFee)/100;
            if (pool.Symbol0 == fromSymbol)
            {
                pool.Amount0 += amount;
                pool.Amount1 -= totalAmountToRemove;
                pool.FeesForOwnerSymbol0 += feeForOwner;
                pool.FeesForUsersSymbol0 += feeForUsers;
            }
            else
            {
                pool.Amount0 -= totalAmountToRemove;
                pool.Amount1 += amount;
                pool.FeesForUsersSymbol1 += feeForUsers;
                pool.FeesForOwnerSymbol1 += feeForOwner;
            }
           
            DistributeFee(feeForUsers, pool, fromSymbol);

            // Save Pool
            _pools.Set<string, Pool>($"{pool.Symbol0}_{pool.Symbol1}", pool);
        }
        
        /// <summary>
        /// To swap tokens in reverse
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="total"></param>
        public void SwapReverse(Address from, string fromSymbol, string toSymbol, BigInteger total)
        {
            var amount = GetRate(toSymbol, fromSymbol, total);
            Runtime.Expect(amount > 0, $"cannot reverse swap {fromSymbol}");
            SwapTokens(from, fromSymbol, toSymbol, amount);
        }
        
        /// <summary>
        /// Swap Fee -> Method used to convert a Symbol into KCAL, Using Pools
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="feeAmount"></param>
        public void SwapFee(Address from, string fromSymbol, BigInteger feeAmount)
        {
            Runtime.Expect(_DEXversion >= 1, "call migrateToV3 first");
            var feeSymbol = DomainSettings.FuelTokenSymbol;

            // Need to remove the fees
            var token = Runtime.GetToken(fromSymbol);
            BigInteger minAmount;
            
            var feeBalance = Runtime.GetBalance(feeSymbol, from);
            feeAmount -= UnitConversion.ConvertDecimals(feeBalance, DomainSettings.FuelTokenDecimals, token.Decimals);
            if (feeAmount <= 0)
            {
                return;
            }

            // different tokens have different decimals, so we need to make sure a certain minimum amount is swapped
            if (token.Decimals == 0)
            {
                minAmount = 1;
            }
            else
            {
                var diff = DomainSettings.FuelTokenDecimals - token.Decimals;
                if (diff > 0)
                {
                    minAmount = BigInteger.Pow(10, diff);
                }
                else
                {
                    minAmount = 1 * BigInteger.Pow(10, token.Decimals);
                }
            }

            if (!PoolIsReal(fromSymbol, feeSymbol))
            {
                var rate = GetRate(fromSymbol, "SOUL", feeAmount);
                SwapTokens(from, fromSymbol, "SOUL", feeAmount);
                SwapTokens(from, "SOUL", feeSymbol, rate);
                return;
            }
            else
            {
                var amountInOtherSymbol = GetRate(feeSymbol, fromSymbol, feeAmount);
                var amountIKCAL = GetRate(fromSymbol, feeSymbol, feeAmount);
                Console.WriteLine($"AmountOther: {amountInOtherSymbol} | feeAmount:{feeAmount} | feeBalance:{feeBalance} | amountOfKcal: {amountIKCAL}" );

                if (amountInOtherSymbol < minAmount)
                {
                    amountInOtherSymbol = minAmount;
                }

                // round up
                //amountInOtherSymbol++;

                SwapTokens(from, fromSymbol, feeSymbol, feeAmount);
            }           

            var finalFeeBalance = Runtime.GetBalance(feeSymbol, from);
            Runtime.Expect(finalFeeBalance >= feeBalance, $"something went wrong in swapFee finalFeeBalance: {finalFeeBalance} feeBalance: {feeBalance}");
        }
        
        /// <summary>
        /// Swap Fiat
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="worth"></param>
        public void SwapFiat(Address from, string fromSymbol, string toSymbol, BigInteger worth)
        {
            var amount = GetRate(DomainSettings.FiatTokenSymbol, fromSymbol, worth);

            var token = Runtime.GetToken(fromSymbol);
            if (token.Decimals == 0 && amount < 1)
            {
                amount = 1;
            }
            else
            {
                Runtime.Expect(amount > 0, $"cannot swap {fromSymbol} based on fiat quote");
            }

            SwapTokens(from, fromSymbol, toSymbol, amount);
        }
        
        /// <summary>
        /// Get the Rate for the trade (with fees included)
        /// </summary>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount">Amount of fromSymbol to Swap</param>
        /// <returns></returns>
        public BigInteger GetRate(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(IsSupportedToken(fromSymbol), "unsupported from symbol");
            Runtime.Expect(IsSupportedToken(toSymbol), $"unsupported to symbol -> {toSymbol}");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(toInfo.IsFungible(), "must be fungible");

            //Runtime.Expect(PoolExists(fromSymbol, toSymbol), $"Pool {fromSymbol}/{toSymbol} doesn't exist.");
            if (!PoolExists(fromSymbol, toSymbol))
            {
                BigInteger rate1 = GetRate(fromSymbol, "SOUL", amount);
                BigInteger rate2 = GetRate("SOUL", toSymbol, rate1);
                return rate2;
            }

            BigInteger rate = 0;

            Pool pool = GetPool(fromSymbol, toSymbol);
            BigInteger tokenAmount = 0;

            //BigInteger power = 0;
            //BigInteger rateForSwap = 0;

            BigInteger feeAmount = pool.FeeRatio;

            bool canBeTraded = false;

            // dy = y * 0.997 * dx /  ( x + 0.997 * dx )
            if (pool.Symbol0 == fromSymbol)
            {
                tokenAmount = pool.Amount1 * (1 - feeAmount / 100) * amount / (pool.Amount0 + (1 - feeAmount / 100) * amount);
                if (toInfo.Decimals == 0)
                    tokenAmount = (int) tokenAmount;
                canBeTraded = ValidateTrade(amount, tokenAmount, pool, true);
                //power = (BigInteger)Math.Pow((long)(pool.Amount0 - amount), 2);
                //rateForSwap = pool.Amount0 * pool.Amount1 * 10000000000 / power;
            }
            else
            {

                tokenAmount = pool.Amount0 * (1-feeAmount / 100) * amount / (pool.Amount1 + (1 - feeAmount / 100) * amount);
                Console.WriteLine($"Test-> {tokenAmount}");
                if (toInfo.Decimals == 0)
                    tokenAmount = (int) tokenAmount;
                canBeTraded = ValidateTrade(tokenAmount, amount, pool, false);
                //canBeTraded = false;
                //power = (BigInteger)Math.Pow((long)(pool.Amount1 + amount), 2);
                //rateForSwap =  pool.Amount0 * pool.Amount1 * 10000000000 / power;
            }

            Console.WriteLine($"Test-> {tokenAmount}");

            rate = tokenAmount;
            Runtime.Expect(canBeTraded, "Can't be traded, the trade is not valid.");
            Runtime.Expect(rate >= 0, "invalid swap rate");

            return rate;
        }

        /// <summary>
        /// Method use to Migrate to the new SwapMechanism
        /// </summary>
        public void MigrateToV3()
        {
            Runtime.Expect(_DEXversion == 0, "Migration failed, wrong version");

            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            Runtime.Expect(existsLP, "LP token doesn't exist!");

            // check how much SOUL we have here
            var soulTotal = Runtime.GetBalance(DomainSettings.StakingTokenSymbol, this.Address);
            
            // creates a new pool for SOUL and every asset that has a balance in v2
            var symbols = Runtime.GetTokens();

            var tokens = new Dictionary<string, BigInteger>();

            // fetch all fungible tokens with balance > 0
            foreach (var symbol in symbols)
            {
                if (symbol == DomainSettings.StakingTokenSymbol)
                {
                    continue;
                }

                var info = Runtime.GetToken(symbol);
                if (info.IsFungible())
                {
                    var balance = Runtime.GetBalance(symbol, this.Address);

                    if (balance > 0)
                    {
                        tokens[symbol] = balance;
                    }
                }
            }

            // sort tokens by estimated SOUL value, from low to high
            var sortedTokens = tokens.Select(x => 
                    new KeyValuePair<string, BigInteger>(x.Key, Runtime.GetTokenQuote(x.Key, DomainSettings.StakingTokenSymbol, x.Value)))
                    //GetRate(x.Key, DomainSettings.StakingTokenSymbol, x.Value)
                .OrderBy(x => x.Value)
                .Select(x => x.Key)
                .ToArray();

            /*var sortedTokens = tokens.Select(x =>
                    new KeyValuePair<string, BigInteger>(x.Key, GetTokenQuote(x.Key)))
                //GetRate(x.Key, DomainSettings.StakingTokenSymbol, x.Value)
                .OrderBy(x => x.Value)
                .Select(x => x.Key)
                .ToArray();*/


            // Calculate the Percent to each Pool
            var tokensPrice = new Dictionary<string, BigInteger>();
            var soulPrice = Runtime.GetTokenQuote(DomainSettings.StakingTokenSymbol, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
            var soulTotalPrice = soulPrice * UnitConversion.ConvertDecimals(soulTotal, DomainSettings.StakingTokenDecimals, DomainSettings.FiatTokenDecimals);
            BigInteger otherTokensTotalValue = 0;
            BigInteger totalTokenAmount = 0;
            BigInteger amount = 0;
            BigInteger tokenPrice = 0;
            BigInteger tokenRatio = 0;
            BigInteger totalPrice = 0;
            BigInteger tokenAmount = 0;
            BigInteger percent = 0;
            BigInteger soulAmount = 0;
            IToken tokenInfo;

            foreach (var symbol in sortedTokens)
            {
                tokenInfo = Runtime.GetToken(symbol);

                amount = UnitConversion.ConvertDecimals(tokens[symbol], tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
                tokenPrice = Runtime.GetTokenQuote(symbol, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, tokenInfo.Decimals));

                //Console.WriteLine($"{symbol} price {tokenPrice}$  .{tokenInfo.Decimals}  { amount}x{tokenPrice} :{ amount * tokenPrice} -> {UnitConversion.ToDecimal(tokenPrice, DomainSettings.FiatTokenDecimals)}");
                tokensPrice[symbol] = tokenPrice;
                otherTokensTotalValue += amount * tokenPrice;
            }

            // Create Pools based on that percent and on the availableSOUL and on token ratio
            BigInteger totalSOULUsed = 0;
            _DEXversion = 1;

            if (otherTokensTotalValue < soulTotalPrice)
            {
                foreach (var symbol in sortedTokens)
                {
                    tokenInfo = Runtime.GetToken(symbol);
                    totalTokenAmount = UnitConversion.ConvertDecimals(tokens[symbol], tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
                    amount = tokens[symbol];
                    soulAmount = tokensPrice[symbol] * totalTokenAmount / soulPrice;
                    //Console.WriteLine($"TokenInfo |-> .{tokenInfo.Decimals} | ${tokensPrice[symbol]} | {tokens[symbol]} {symbol} | Converted {amount} {symbol} ");
                    //Console.WriteLine($"TradeValues |-> {percent}% | {tokenAmount} | {soulAmount}/{soulTotal}\n");
                    //Console.WriteLine($"{symbol} |-> .{tokenInfo.Decimals} | ${UnitConversion.ToDecimal(tokensPrice[symbol], DomainSettings.FiatTokenDecimals)} | {UnitConversion.ToDecimal(tokens[symbol], tokenInfo.Decimals)} {symbol} | Converted {UnitConversion.ToDecimal(amount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"{symbol} Ratio |-> {tokenRatio}% | {UnitConversion.ToDecimal(amount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"SOUL |-> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)}/{UnitConversion.ToDecimal(soulTotal, DomainSettings.StakingTokenDecimals)} | {soulPrice} | {soulTotalPrice}");
                    //Console.WriteLine($"TradeValues |-> {percent}% | {amount} | {soulAmount}/{soulTotal} -> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL");
                    //Console.WriteLine($"Trade {UnitConversion.ToDecimal(amount, tokenInfo.Decimals)} {symbol} for {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL\n");
                    totalSOULUsed += soulAmount;
                    CreatePool(this.Address, DomainSettings.StakingTokenSymbol, soulAmount, symbol, amount);
                }
            }
            else
            {
                // With Price Ratio
                foreach (var symbol in sortedTokens)
                {
                    tokenInfo = Runtime.GetToken(symbol);
                    totalTokenAmount = UnitConversion.ConvertDecimals(tokens[symbol], tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
                    amount = 0;
                    percent = tokensPrice[symbol] * totalTokenAmount * 100 / otherTokensTotalValue;
                    soulAmount = UnitConversion.ConvertDecimals((soulTotal * percent / 100), DomainSettings.FiatTokenDecimals, DomainSettings.StakingTokenDecimals);

                    tokenRatio = tokensPrice[symbol] / soulPrice;
                    if ( tokenRatio != 0)
                        tokenAmount = UnitConversion.ConvertDecimals((soulAmount / tokenRatio), DomainSettings.FiatTokenDecimals, tokenInfo.Decimals);
                    else
                    {
                        tokenRatio = soulPrice/tokensPrice[symbol];
                        tokenAmount = UnitConversion.ConvertDecimals((soulAmount * tokenRatio), DomainSettings.FiatTokenDecimals, tokenInfo.Decimals);
                    }

                    //Console.WriteLine($"{symbol} |-> .{tokenInfo.Decimals} | ${UnitConversion.ToDecimal(tokensPrice[symbol], DomainSettings.FiatTokenDecimals)} | {UnitConversion.ToDecimal(tokens[symbol], tokenInfo.Decimals)} {symbol} | Converted {UnitConversion.ToDecimal(tokenAmount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"{symbol} Ratio |-> {tokenRatio}% | {UnitConversion.ToDecimal(tokenAmount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"SOUL |-> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)}/{UnitConversion.ToDecimal(soulTotal, DomainSettings.StakingTokenDecimals)} | {soulPrice} | {soulTotalPrice}");
                    //Console.WriteLine($"TradeValues |-> {percent}% | {tokenAmount} | {soulAmount}/{soulTotal} -> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL");
                    //Console.WriteLine($"Trade {UnitConversion.ToDecimal(tokenAmount, tokenInfo.Decimals)} {symbol} for {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL\n");
                    totalSOULUsed += soulAmount;
                    Runtime.Expect(soulAmount <= soulTotal, $"SOUL higher than total... {soulAmount}/{soulTotal}");
                    CreatePool(this.Address, DomainSettings.StakingTokenSymbol, soulAmount, symbol, tokenAmount);
                    Runtime.TransferTokens(symbol, this.Address, SmartContract.GetAddressForNative(NativeContractKind.Swap), tokens[symbol] - tokenAmount);
                }
            }

            Runtime.Expect(totalSOULUsed <= soulTotal, "Used more than it has...");

            // return the left overs
            Runtime.TransferTokens(DomainSettings.StakingTokenSymbol, this.Address, SmartContract.GetAddressForNative(NativeContractKind.Swap), soulTotal - totalSOULUsed);
        }        
        
        
        #region DEXify
        public const string ExhcangeDexMinimumFeeTag = "exchange.dex.minimum.fee";
        public const string ExhcangeDexDefaultFeeTag = "exchange.dex.default.fee";
        public static readonly BigInteger ExhcangeDexMinimumFee = 1; // 0.01% - for total fees of the tx
        public static readonly BigInteger ExhcangeDexDefaultFee = 3; // 0.03% - for total fees of the tx
        
        public const string ExchangeDexPoolPercentTag = "exchange.dex.pool.percent";
        public static readonly BigInteger ExchangeDexDefaultPoolPercent = 75; // 75% of the tx's goes to the LP Providers
        
        public const string ExchangeDexGovernancePercentTag = "exchange.dex.governance.percent";
        public static readonly BigInteger ExchangeDexDefaultGovernancePercent = 25; // 75% of the tx's goes to the LP Providers
        
        // value in "per thousands"
        private const int DEXSeriesID = 0; 
        internal StorageMap _pools;
        internal StorageMap _lp_tokens; // <string, BigInteger>
        internal StorageMap _lp_holders; // <string, storage_list<Address>> |-> string : $"symbol0_symbol1" |-> Address[] : key to the list 
        internal StorageMap _trading_volume; // <string, stoage_map<uint,TradingVolume>> |-> string : $"symbol0_symbol1" |-> TradingVolume[] : key to the list 

        //Runtime.GetGovernanceValue(ValidatorSlotsTag);
        
        #region Trading Volume
        /// <summary>
        /// Get the storga map of the tradingvolume
        /// </summary>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private StorageMap GetTradingVolume(string symbol0, string symbol1)
        {
            string key = $"{symbol0}_{symbol1}";
            if (!_trading_volume.ContainsKey<string>(key))
            {
                StorageMap newStorage = new StorageMap();
                _trading_volume.Set<string, StorageMap>(key, newStorage);
            }

            var _tradingList = _trading_volume.Get<string, StorageMap>(key);
            return _tradingList;
        }
        
        // Year -> Math.floor(((time % 31556926) % 2629743) / 86400)
        // Month -> Math.floor(((time % 31556926) % 2629743) / 86400)
        // Day -> Math.floor(((time % 31556926) % 2629743) / 86400)
        public TradingVolume GetTradingVolumeToday(string symbol0, string symbol1)
        {
            var tradingMap = GetTradingVolume(symbol0, symbol1);
            var today = DateTime.Today.Date;
            var todayTimestamp = (Timestamp)today;
            
            TradingVolume tempTrading = new TradingVolume(symbol0, symbol0, today.ToShortDateString(), 0, 0);
            
            if ( tradingMap.ContainsKey(todayTimestamp))
                return tradingMap.Get<Timestamp, TradingVolume>(todayTimestamp);
            return tempTrading;
        }

        /// <summary>
        /// Get all the trading volumes for that Pool
        /// </summary>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        public TradingVolume[] GetTradingVolumes(string symbol0, string symbol1)
        {
            var tradingMap = GetTradingVolume(symbol0, symbol1);
            return tradingMap.AllValues<TradingVolume>();
        }

        /// <summary>
        /// Update the trading volume
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="Amount">Always in SOUL AMOUNT!</param>
        private void UpdateTradingVolume(Pool pool, string fromSymbol, BigInteger Amount)
        {
            var today = DateTime.Today.Date; 
            var todayTimestamp = (Timestamp)today;
            StorageMap tradingMap = GetTradingVolume(pool.Symbol0, pool.Symbol1);
            var tradingToday = GetTradingVolumeToday(pool.Symbol0, pool.Symbol1);
            string key = $"{pool.Symbol0}_{pool.Symbol1}";
            if (fromSymbol == pool.Symbol0)
                tradingToday.VolumeSymbol0 += Amount;
            else 
                tradingToday.VolumeSymbol1 += Amount;

            tradingMap.Set(todayTimestamp, tradingToday);
            
            _trading_volume.Set<string, StorageMap>(key, tradingMap);
        }
        #endregion
        
        #region LP Holder
        /// <summary>
        /// This method is used to generate the key related to the USER NFT ID, to make it easier to fetch.
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns>Get LP Tokens Key</returns>
        private string GetLPTokensKey(Address from, string symbol0, string symbol1) {
            return $"{from.Text}_{symbol0}_{symbol1}";
        }

        /// <summary>
        /// Get the Holders AddressList for a Specific Pool
        /// </summary>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private StorageList GetHolderList(string symbol0, string symbol1)
        {
            string key = $"{symbol0}_{symbol1}";
            if (!_lp_holders.ContainsKey<string>(key))
            {
                StorageList newStorage = new StorageList();
                _lp_holders.Set<string, StorageList>(key, newStorage);
            }

            var _holderList = _lp_holders.Get<string, StorageList>(key);
            return _holderList;
        }

        /// <summary>
        /// This method is used to check if the user already has LP on the pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns>True or False depending if the user has it or not</returns>
        private bool UserHasLP(Address from, string symbol0, string symbol1)
        {
            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");
            // Check if a pool exist (token0 - Token 1) and (token 1 - token 0)

            if (_lp_tokens.ContainsKey(GetLPTokensKey(from, symbol0, symbol1)) || _lp_tokens.ContainsKey(GetLPTokensKey(from, symbol1, symbol0)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get LP Holder by Address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private LPHolderInfo GetLPHolder(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(CheckHolderIsThePool(from, symbol0, symbol1), "User is not on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo tempHolder = new LPHolderInfo();
            while (index < count)
            {
                tempHolder = holdersList.Get<LPHolderInfo>(index);
                if (tempHolder.Address == from)
                    return tempHolder;

                index++;
            }
            return tempHolder;
        }

        /// <summary>
        /// Check if the holder is on the pool for the fees.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private bool CheckHolderIsThePool(Address from, string symbol0, string symbol1)
        {
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo tempHolder;
            while ( index < count)
            {
                tempHolder = holdersList.Get<LPHolderInfo>(index);
                if (tempHolder.Address == from)
                    return true;

                index++;
            }
            return false;
        }

        /// <summary>
        /// Add to LP Holder for that Pool
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void AddToLPHolders(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(!CheckHolderIsThePool(from, symbol0, symbol1), "User is already on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var lpHolderInfo = new LPHolderInfo(from, 0, 0, 0, 0);
            holdersList.Add<LPHolderInfo>(lpHolderInfo);
            _lp_holders.Set<string, StorageList>($"{symbol0}_{symbol1}", holdersList);
        }

        /// <summary>
        /// Update LP Holder for that specific pool
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void UpdateLPHolders(LPHolderInfo holder, string symbol0, string symbol1)
        {
            Runtime.Expect(CheckHolderIsThePool(holder.Address, symbol0, symbol1), "User is not on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo tempHolder = new LPHolderInfo();

            while (index < count)
            {
                tempHolder = holdersList.Get<LPHolderInfo>(index);
                if (tempHolder.Address == holder.Address)
                {
                    holdersList.Replace<LPHolderInfo>(index, holder);
                    _lp_holders.Set<string, StorageList>($"{symbol0}_{symbol1}", holdersList);
                    break;
                }
                index++;
            }
        }

        /// <summary>
        /// Remove From the LP Holder for that specific pool
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void RemoveFromLPHolders(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(CheckHolderIsThePool(from, symbol0, symbol1), "User is not on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo lpHolderInfo = new LPHolderInfo();

            while (index < count)
            {
                lpHolderInfo = holdersList.Get<LPHolderInfo>(index);
                if (lpHolderInfo.Address == from)
                {
                    holdersList.RemoveAt(index);
                    _lp_holders.Set<string, StorageList>($"{symbol0}_{symbol1}", holdersList);
                    break;
                }
                index++;
            }            
        }

        /// <summary>
        /// This method is to add the NFT ID to the list of NFT in that pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="NFTID">NFT ID</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        private void AddToLPTokens(Address from, BigInteger NFTID, string symbol0, string symbol1)
        {
            var lptokenKey = GetLPTokensKey(from, symbol0, symbol1);
            _lp_tokens.Set<string, BigInteger>(lptokenKey, NFTID);
            AddToLPHolders(from, symbol0, symbol1);
        }

        /// <summary>
        /// Update User 
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <param name="claimedAmountSymbol0"></param>
        /// <param name="claimedAmountSymbol1"></param>
        private void UpdateUserLPToken(Address from, string symbol0, string symbol1, BigInteger claimedAmountSymbol0, BigInteger claimedAmountSymbol1)
        {
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");
            Pool pool = GetPool(symbol0, symbol1);
            Runtime.Expect(UserHasLP(from, pool.Symbol0, pool.Symbol1), $"User doesn't have LP");
            var lpKey = GetLPTokensKey(from, pool.Symbol0, pool.Symbol1);
            Runtime.Expect(_lp_tokens.ContainsKey(lpKey), "Doesn't contain");
            var nftID = _lp_tokens.Get<string, BigInteger>(lpKey);
            var ram = GetMyPoolRAM(from, pool.Symbol0, pool.Symbol1);
            ram.ClaimedFeesSymbol0 += claimedAmountSymbol0;
            ram.ClaimedFeesSymbol1 += claimedAmountSymbol1;
            Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(ram).AsByteArray());
        }


        /// <summary>
        /// This method is used to Remove user From LP Tokens
        /// </summary>
        /// <param name="from"></param>
        /// <param name="NFTID"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void RemoveFromLPTokens(Address from, BigInteger NFTID, string symbol0, string symbol1)
        {
            var lptokenKey = GetLPTokensKey(from, symbol0, symbol1);
            Runtime.Expect(_lp_tokens.ContainsKey<string>(lptokenKey), "The user is not on the list.");
            _lp_tokens.Remove<string>(lptokenKey);
            RemoveFromLPHolders(from, symbol0, symbol1);
        }
        #endregion
        
        #region Pool Related
        /// <summary>
        /// This method is used to check if the pool is Real or Virtual.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns></returns>
        private bool PoolIsReal(string symbol0, string symbol1)
        {
            return symbol0 == DomainSettings.StakingTokenSymbol || symbol1 == DomainSettings.StakingTokenSymbol;
        }

        public LPTokenContentRAM GetMyPoolRAM(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");
            Pool pool = GetPool(symbol0, symbol1);
            Runtime.Expect(UserHasLP(from, pool.Symbol0, pool.Symbol1), $"User doesn't have LP");
            var lpKey = GetLPTokensKey(from, pool.Symbol0, pool.Symbol1);
            Runtime.Expect(_lp_tokens.ContainsKey(lpKey), "Doesn't contain");
            var nftID = _lp_tokens.Get<string, BigInteger>(lpKey);
            var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
            LPTokenContentRAM nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();
            return nftRAM;
        }

        /// <summary>
        /// This method is used to get all the pools.
        /// </summary>
        /// <returns>Array of Pools</returns>
        public Pool[] GetPools()
        {
            return _pools.AllValues<Pool>();
        }

        /// <summary>
        /// This method is used to get a specific pool.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns>Pool</returns>
        public Pool GetPool(string symbol0, string symbol1)
        {
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            if (_pools.ContainsKey($"{symbol0}_{symbol1}"))
            {
                return _pools.Get<string, Pool>($"{symbol0}_{symbol1}");
            }

            if (_pools.ContainsKey($"{symbol1}_{symbol0}"))
            {
                return _pools.Get<string, Pool>($"{symbol1}_{symbol0}");
            }

            return _pools.Get<string, Pool>($"{symbol0}_{symbol1}");
        }

        /// <summary>
        /// This method is used to check if a pool exists or not.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2ndToken</param>
        /// <returns>True or false</returns>
        private bool PoolExists(string symbol0, string symbol1)
        {
            // Check if the tokens exist
            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            // Check if a pool exist (token0 - Token 1) and (token 1 - token 0)
            if ( _pools.ContainsKey($"{symbol0}_{symbol1}") || _pools.ContainsKey($"{symbol1}_{symbol0}") ){
                return true;
            }

            return false;
        }
        
        

        /// <summary>
        /// This method is Used to create a Pool.
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void CreatePool(Address from, string symbol0, BigInteger amount0, string symbol1,  BigInteger amount1)
        {
            Runtime.Expect(_DEXversion >= 1, "call migrateV3 first");

            // Check the if the input is valid
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount 0");
            //Runtime.Expect(symbol0 == "SOUL" || symbol1 == "SOUL", "Virtual pools are not supported yet!");

            // Check if pool exists
            Runtime.Expect(!PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");
            
            var symbol0Price = Runtime.GetTokenQuote(symbol0, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, token0Info.Decimals));
            var symbol1Price = Runtime.GetTokenQuote(symbol1, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, token1Info.Decimals)); //GetTokenQuote(symbol1)

            BigInteger sameDecimalsAmount0 =  UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger sameDecimalsAmount1 =  UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            
            decimal tradeRatio = 0;
            decimal tradeRatioAmount = 0;
            
            if (symbol0Price == 0 || symbol1Price == 0)
            {
                // Use own ratio
                Runtime.Expect(amount0 != 0 && amount1 != 0, "Amount must be different from 0, if there's no symbol price.");
                tradeRatio = GetAmountRatio(amount0, token0Info, amount1, token1Info);
            }
            else
            {
                // Both amounts set, the user created the ration when creating the pool
                if (amount0 != 0 && amount1 != 0)
                {
                    tradeRatio = GetAmountRatio(amount0, token0Info, amount1, token1Info);
                }
                else
                {
                    // Check ratio based on Token Price
                    tradeRatio = decimal.Round(UnitConversion.ToDecimal(symbol0Price,  DomainSettings.FiatTokenDecimals)  / UnitConversion.ToDecimal(symbol1Price, DomainSettings.FiatTokenDecimals), DomainSettings.MAX_TOKEN_DECIMALS/2, MidpointRounding.AwayFromZero );
                    
                    if ( amount0 == 0 )
                    {
                        amount0 = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.FiatTokenDecimals) / tradeRatio), token0Info.Decimals);
                    }
                    else if (amount1 == 0)
                    {
                        amount1 = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals) / tradeRatio), token1Info.Decimals);
                    }
                    
                }
                
                sameDecimalsAmount0 =  UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
                sameDecimalsAmount1 =  UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            }
            
            Runtime.Expect( tradeRatio > 0, $"TradeRatio > 0 | {tradeRatio} > 0");
            
            Runtime.Expect( ValidateRatio(sameDecimalsAmount0, sameDecimalsAmount1, tradeRatio), $"ratio is not true. {tradeRatio}, new {sameDecimalsAmount0} {sameDecimalsAmount1} {sameDecimalsAmount0 / sameDecimalsAmount1} {amount0/ amount1}");

            var symbol0Balance = Runtime.GetBalance(symbol0, from);
            Runtime.Expect(symbol0Balance >= amount0, $"not enough {symbol0} balance, you need {amount0}");
            var symbol1Balance = Runtime.GetBalance(symbol1, from);
            Runtime.Expect(symbol1Balance >= amount1, $"not enough {symbol1} balance, you need {amount1}");


            BigInteger feeRatio = (amount0 * ExhcangeDexDefaultFee) / 1000;
            feeRatio = ExhcangeDexDefaultFee;

            // Get the token address
            // Token0 Address
            Address token0Address = TokenUtils.GetContractAddress(symbol0);

            // Token1 Address
            Address token1Address = TokenUtils.GetContractAddress(symbol1);            

            BigInteger TLP = Sqrt(sameDecimalsAmount0 * sameDecimalsAmount1) * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);

            // Create the pool
            Pool pool = new Pool(symbol0, symbol1, token0Address.Text, token1Address.Text, amount0, amount1, feeRatio, TLP);

            _pools.Set<string, Pool>($"{symbol0}_{symbol1}", pool);

            // Give LP Token to the address
            LPTokenContentROM nftROM = new LPTokenContentROM(pool.Symbol0, pool.Symbol1, Runtime.GenerateUID());
            LPTokenContentRAM nftRAM = new LPTokenContentRAM(amount0, amount1, TLP);

            // Mint Token
            var nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, this.Address, from, VMObject.FromStruct(nftROM).AsByteArray(), VMObject.FromStruct(nftRAM).AsByteArray(), DEXSeriesID);
            Runtime.TransferTokens(pool.Symbol0, from, this.Address, amount0);
            Runtime.TransferTokens(pool.Symbol1, from, this.Address, amount1);
            AddToLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
        }

        /// <summary>
        /// This method is used to Provide Liquidity to the Pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void AddLiquidity(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            // Check input
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 >= 0, "invalid amount 0");
            Runtime.Expect(amount1 >= 0, "invalid amount 1");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount, both amounts can't be 0");
            Runtime.Expect(symbol0 != symbol1, "Symbols are the same...");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");
            
            // Check if pool exists
            if (!PoolExists(symbol0, symbol1))
            {
                CreatePool(from, symbol0, amount0, symbol1, amount1);
                return;
            }

            // Get pool
            Pool pool = GetPool(symbol0, symbol1);
            decimal poolRatio = 0;
            decimal tradeRatioAmount = 0;

            // Fix inputs
            if (symbol0 != pool.Symbol0)
            {
                symbol0 = pool.Symbol0;
                symbol1 = pool.Symbol1;
                (token0Info, token1Info) = (token1Info, token0Info);
                (amount0, amount1) = (amount1, amount0);
            }
            
            // Pool Amounts Same Decimals
            BigInteger poolSameDecimalsAmount0 =  UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger poolSameDecimalsAmount1 =  UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            
            BigInteger sameDecimalsAmount0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger sameDecimalsAmount1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            // Get the Pool Ratio
            poolRatio = GetPoolRatio(pool, token0Info, token1Info);
            
            // Calculate Amounts if they are 0
            if (amount0 == 0)
            {
                amount0 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.FiatTokenDecimals) / poolRatio, token0Info.Decimals);
            }
            else
            {
                if (amount1 == 0)
                {
                    amount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals)  / poolRatio, token1Info.Decimals);
                }
            }
            
            sameDecimalsAmount0 =  UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.StakingTokenDecimals);
            sameDecimalsAmount1 =  UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.StakingTokenDecimals);
            
            // Get Trading Ratio
            tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);
                
            if (poolRatio == 0)
            {
                poolRatio = tradeRatioAmount;
            }
            else
            {
                //if (tradeRatioAmount == 0)
                //    tradeRatioAmount = poolRatio;
                
                // If the ratio's differ it means that the amount in is not true to the pool ratio.
                if (tradeRatioAmount != poolRatio)
                {
                    amount1 = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals)  / poolRatio), token1Info.Decimals);

                    sameDecimalsAmount0 =  UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
                    sameDecimalsAmount1 =  UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
                    
                    tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);
                }
                
                Runtime.Expect(tradeRatioAmount == poolRatio, $"TradeRatio < 0 | {poolRatio} != {tradeRatioAmount}");
            }
            
            // Validate the Ratio
            Runtime.Expect(ValidateRatio(sameDecimalsAmount0, sameDecimalsAmount1, poolRatio), $"ratio is not true. {poolRatio}, new {sameDecimalsAmount0} {sameDecimalsAmount1} {sameDecimalsAmount1  / sameDecimalsAmount0} {amount1  /amount0 }");

            //Console.WriteLine($"ADD: ratio:{poolRatio} | amount0:{amount0} | amount1:{amount1}");
            BigInteger liquidity = 0;
            BigInteger nftID = 0;
            LPTokenContentROM nftROM = new LPTokenContentROM(pool.Symbol0, pool.Symbol1, Runtime.GenerateUID());
            LPTokenContentRAM nftRAM = new LPTokenContentRAM();

            // Check if user has the LP Token and Update the values
            if (UserHasLP(from, pool.Symbol0, pool.Symbol1))
            {
                // Update the NFT VALUES
                var lpKey = GetLPTokensKey(from, pool.Symbol0, pool.Symbol1);
                nftID = _lp_tokens.Get<string, BigInteger>(lpKey);
                var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
                nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();

                // CALCULATE BASED ON THIS lp_amount = (SOUL_USER  * LP_TOTAL )/  SOUL_TOTAL
                // TODO: calculate the amounts according to the ratio...

                liquidity = (sameDecimalsAmount0 * pool.TotalLiquidity) / poolSameDecimalsAmount0; //* UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);

                nftRAM.Amount0 += amount0;
                nftRAM.Amount1 += amount1;
                nftRAM.Liquidity += liquidity;

                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());
            }
            else
            {
                // MINT NFT and give to the user
                // CALCULATE BASED ON THIS lp_amount = (SOUL_USER  * LP_TOTAL )/  SOUL_TOTAL
                liquidity = (sameDecimalsAmount0 * pool.TotalLiquidity / poolSameDecimalsAmount0); //*UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);
                nftRAM = new LPTokenContentRAM(amount0, amount1, liquidity);

                nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, this.Address, from, VMObject.FromStruct(nftROM).AsByteArray(), VMObject.FromStruct(nftRAM).AsByteArray(), DEXSeriesID);
                AddToLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
            }

            // Update the pool values
            Runtime.TransferTokens(pool.Symbol0, from, this.Address, amount0);
            Runtime.TransferTokens(pool.Symbol1, from, this.Address, amount1);
            pool.Amount0 += amount0;
            pool.Amount1 += amount1;
            pool.TotalLiquidity += liquidity;

            _pools.Set<string, Pool>($"{pool.Symbol0}_{pool.Symbol1}", pool);
        }

        /// <summary>
        /// This method is used to Remove Liquidity from the pool.
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void RemoveLiquidity(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            // Check input
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 >= 0 , "invalid amount 0");
            Runtime.Expect(amount1 >= 0 , "invalid amount 1");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount, both amounts can't be 0");

            // Check if user has LP Token
            Runtime.Expect(UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            // Get Pool
            Pool pool = GetPool(symbol0, symbol1);
            BigInteger liquidity = 0;

            // Fix inputs
            if (symbol0 != pool.Symbol0)
            {
                symbol0 = pool.Symbol0;
                symbol1 = pool.Symbol1;
                (token0Info, token1Info) = (token1Info, token0Info);
                (amount0, amount1) = (amount1, amount0);
            }
            
            // Pool Amounts Same Decimals
            BigInteger poolSameDecimalsAmount0 =  UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger poolSameDecimalsAmount1 =  UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            
            BigInteger sameDecimalsAmount0 =  UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger sameDecimalsAmount1 =  UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            // Calculate Amounts
            decimal poolRatio = 0; 
            decimal tradeRatioAmount = 0;
            
            poolRatio = GetPoolRatio(pool, token0Info, token1Info);
            
            // Calculate Amounts if they are 0
            if (amount0 == 0)
            {
                amount0 = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(sameDecimalsAmount1, DomainSettings.FiatTokenDecimals)  / poolRatio), token0Info.Decimals);
            }
            else  if (amount1 == 0)
            {
                amount1 = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals)  / poolRatio), token1Info.Decimals);
            }

            // To the same Decimals for ease of calculation
            sameDecimalsAmount0 =  UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            sameDecimalsAmount1 =  UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);

            tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);

            if (poolRatio == 0)
            {
                poolRatio = tradeRatioAmount;
            }
            else
            {
                if (tradeRatioAmount != poolRatio)
                {
                    amount1 = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(sameDecimalsAmount0, DomainSettings.FiatTokenDecimals)  / poolRatio), token1Info.Decimals);
                    
                    sameDecimalsAmount0 =  UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
                    sameDecimalsAmount1 =  UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
                    
                    tradeRatioAmount = GetAmountRatio(amount0, token0Info, amount1, token1Info);

                }
                
                Runtime.Expect(tradeRatioAmount == poolRatio, $"TradeRatio < 0 | {poolRatio} != {tradeRatioAmount}");
            }

            //Console.WriteLine($"pool:{poolRatio} | trade:{tradeRatioAmount} | {amount0} {symbol0} for {amount1} {symbol1}");
            Runtime.Expect(ValidateRatio(sameDecimalsAmount0, sameDecimalsAmount1, poolRatio), $"ratio is not true. {poolRatio}, new {sameDecimalsAmount0} {sameDecimalsAmount1} {sameDecimalsAmount0 / sameDecimalsAmount1} {amount0 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals)}");

            // Update the user NFT
            var lpKey = GetLPTokensKey(from, pool.Symbol0, pool.Symbol1);
            var nftID = _lp_tokens.Get<string, BigInteger>(lpKey);
            var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
            LPTokenContentRAM nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();
            BigInteger oldAmount0 = nftRAM.Liquidity * pool.Amount0 / pool.TotalLiquidity;
            BigInteger oldAmount1 = nftRAM.Liquidity * pool.Amount1 / pool.TotalLiquidity;
            BigInteger newAmount0 = oldAmount0 - amount0;
            BigInteger newAmount1 = oldAmount1 - amount1;
            BigInteger oldLP = nftRAM.Liquidity;
            Console.WriteLine($"new LP Division? : {(pool.Amount0-nftRAM.Amount0)}");
            Console.WriteLine($"Pool Amount : {(pool.Amount0)} | NFT -> {nftRAM.Amount0}");
            Console.WriteLine($"Old Amount 0 : {(oldAmount0)} |  1 -> {oldAmount1}");
            BigInteger division = pool.Amount0 - nftRAM.Amount0;
            BigInteger lp = pool.TotalLiquidity - nftRAM.Liquidity;
            
            if (pool.Amount0 - nftRAM.Amount0 <= 0)
            {
                division = 1;
            }
            
            // To make math possible.
            if (lp == 0)
            {
                lp = 1;
            }
            
            BigInteger newLiquidity = 0;
            if (pool.Amount0 == oldAmount0)
            {
                newLiquidity = (BigInteger) Sqrt(newAmount0 * newAmount1);
            }
            else
            {
                newLiquidity = newAmount0 * (lp) / division;
            }
            

            Console.WriteLine($"LP Division? : {(pool.Amount0)}");

            liquidity = (amount0 * pool.TotalLiquidity) / (pool.Amount0);
            
            Runtime.Expect(nftRAM.Liquidity - liquidity >= 0, "Trying to remove more than you have...");

            Console.WriteLine($"BeforeLP:{nftRAM.Liquidity} - LiquidityToRemove:{liquidity} | FinalLP:{newLiquidity}");


            // If the new amount will be = 0 then burn the NFT
            if (newAmount0 == 0)
            {
                // Burn NFT
                Runtime.BurnToken(DomainSettings.LiquidityTokenSymbol, from, nftID);
                RemoveFromLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
                Runtime.TransferTokens(pool.Symbol0, this.Address, from, oldAmount0);
                Runtime.TransferTokens(pool.Symbol1, this.Address, from, oldAmount1);
            }
            else
            {
                Runtime.Expect(oldAmount0 - amount0 > 0, $"Lower Amount for symbol {symbol0}. | You have {oldAmount0} {symbol0}, trying to remove {amount0} {symbol0}");
                nftRAM.Amount0 = newAmount0;

                Runtime.Expect(oldAmount1 - amount1 > 0, $"Lower Amount for symbol {symbol1}. | You have {oldAmount1} {symbol1}, trying to remove {amount1} {symbol1}");
                nftRAM.Amount1 = newAmount1;

                Runtime.TransferTokens(symbol0, this.Address, from, amount0);
                Runtime.TransferTokens(symbol1, this.Address, from, amount1);
                
                nftRAM.Liquidity = newLiquidity;

                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());
            }

            // Update the pool values
            if (pool.Amount0 == oldAmount0)
            {
                pool.Amount0 = newAmount0;
                pool.Amount1 = newAmount1;
                pool.TotalLiquidity = newLiquidity;
            }
            else
            {
                pool.Amount0 = (pool.Amount0 - oldAmount0) + newAmount0;
                pool.Amount1 = (pool.Amount1 - oldAmount1) + newAmount1;
                pool.TotalLiquidity = pool.TotalLiquidity - oldLP + newLiquidity;
            }

            if (pool.Amount0 == 0)
            {
                _pools.Remove($"{pool.Symbol0}_{pool.Symbol1}");
            }
            else
            {
                _pools.Set<string, Pool>($"{pool.Symbol0}_{pool.Symbol1}", pool);
            }
        }
        
        #endregion
        
        #region Checkers
        /// <summary>
        /// Validate Trade
        /// </summary>
        /// <param name="amount0"></param>
        /// <param name="amount1"></param>
        /// <param name="pool"></param>
        /// <param name="isBuying"></param>
        /// <returns></returns>
        private bool ValidateTrade(BigInteger amount0, BigInteger amount1, Pool pool, bool isBuying = false)
        {
            if (isBuying)
            {
                if (pool.Amount0 + amount0 > 0 && pool.Amount1 - amount1 > 0)
                    return true;
            }
            else
            {
                if (pool.Amount0 - amount0 > 0 && pool.Amount1 + amount1 > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Validate Ratio
        /// </summary>
        /// <param name="amount0"></param>
        /// <param name="amount1"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private bool ValidateRatio(BigInteger amount0, BigInteger amount1, decimal ratio)
        {
            decimal sameDecimalsAmount0 =  UnitConversion.ToDecimal(amount0, DomainSettings.FiatTokenDecimals);
            decimal sameDecimalsAmount1 =  UnitConversion.ToDecimal(amount1, DomainSettings.FiatTokenDecimals);

            if ( sameDecimalsAmount1 > 0)
                return decimal.Round(sameDecimalsAmount0 / sameDecimalsAmount1, DomainSettings.MAX_TOKEN_DECIMALS/2, MidpointRounding.AwayFromZero ) == ratio;
            return false;
        }

        /// <summary>
        /// Calculate fee's to LP on the pool
        /// </summary>
        /// <param name="totalFee"></param>
        /// <param name="liquidity"></param>
        /// <param name="totalLiquidity"></param>
        /// <returns></returns>
        private BigInteger CalculateFeeForUser(BigInteger totalFee, BigInteger liquidity, BigInteger totalLiquidity)
        {
            BigInteger feeAmount = liquidity * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS) / totalLiquidity;
            return totalFee * feeAmount / UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);
        }

        /// <summary>
        /// Get the pool ratio
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="token0Info"></param>
        /// <param name="token1Info"></param>
        /// <returns></returns>
        private decimal GetPoolRatio(Pool pool, IToken token0Info, IToken token1Info)
        {
            BigInteger poolSameDecimalsAmount0 =  UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger poolSameDecimalsAmount1 =  UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            if ( poolSameDecimalsAmount1 > 0)
                return decimal.Round((UnitConversion.ToDecimal(poolSameDecimalsAmount0, DomainSettings.FiatTokenDecimals) / UnitConversion.ToDecimal(poolSameDecimalsAmount1, DomainSettings.FiatTokenDecimals)), DomainSettings.MAX_TOKEN_DECIMALS/2, MidpointRounding.AwayFromZero);
            return 0;
        }

        private decimal GetAmountRatio(BigInteger amount0, IToken token0Info, BigInteger amount1, IToken token1Info)
        {
            BigInteger amount0SameDecimals =  UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            BigInteger amount1SameDecimals =  UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            if (amount1 > 0)
                return decimal.Round((UnitConversion.ToDecimal(amount0SameDecimals, DomainSettings.FiatTokenDecimals) / UnitConversion.ToDecimal(amount1SameDecimals, DomainSettings.FiatTokenDecimals)), DomainSettings.MAX_TOKEN_DECIMALS/2, MidpointRounding.AwayFromZero);
            return 0;
        }
        #endregion

        /// <summary>
        /// Distribute Fees
        /// </summary>
        /// <param name="totalFeeAmount"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void DistributeFee(BigInteger totalFeeAmount, Pool pool, string symbolDistribute)
        {
            Runtime.Expect(PoolExists(pool.Symbol0, pool.Symbol1), $"Pool {pool.Symbol0}/{pool.Symbol1} doesn't exist.");
            var holdersList = GetHolderList(pool.Symbol0, pool.Symbol1);
            var index = 0;
            var count = holdersList.Count();
            var feeAmount = totalFeeAmount; 
            BigInteger amount = 0;
            LPHolderInfo holder = new LPHolderInfo();
            LPTokenContentRAM nftRAM = new LPTokenContentRAM();

            while (index < count)
            {
                holder = holdersList.Get<LPHolderInfo>(index);
                nftRAM = GetMyPoolRAM(holder.Address, pool.Symbol0, pool.Symbol1);
                amount = CalculateFeeForUser(totalFeeAmount, nftRAM.Liquidity, pool.TotalLiquidity);
                if ( holder.Address != this.Address)
                    Runtime.Expect(amount > 0, $"Amount failed for user: {holder.Address}, amount:{amount}, feeAmount:{feeAmount}, feeTotal:{totalFeeAmount}");
                
                feeAmount -= amount;
                if (pool.Symbol0 == symbolDistribute)
                    holder.UnclaimedSymbol0 += amount;
                else
                    holder.UnclaimedSymbol1 += amount;
                
                holdersList.Replace<LPHolderInfo>(index, holder);

                index++;
            }
            
            Runtime.Expect(feeAmount <= 1, $"{feeAmount} was > than 0");

            // Update List
            _lp_holders.Set<string, StorageList>($"{pool.Symbol0}_{pool.Symbol1}", holdersList);
        }

        #region LP Holder Interactions
        /// <summary>
        /// Method used to claim fees
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        public void ClaimFees(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            // Check if user has LP Token
            Runtime.Expect(UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var holder = GetLPHolder(from, symbol0, symbol1);
            var unclaimedAmount0 = holder.UnclaimedSymbol0;
            var unclaimedAmount1 = holder.UnclaimedSymbol1;

            Runtime.TransferTokens(symbol0, this.Address, from, unclaimedAmount0);
            Runtime.TransferTokens(symbol1, this.Address, from, unclaimedAmount1);

            holder.ClaimedSymbol0 += unclaimedAmount0;
            holder.ClaimedSymbol1 += unclaimedAmount1;
            holder.UnclaimedSymbol0 = 0;
            holder.UnclaimedSymbol1 = 0;

            // Update LP Holder
            UpdateLPHolders(holder, symbol0, symbol1);

            // Update NFT
            UpdateUserLPToken(from, symbol0, symbol1, unclaimedAmount0, unclaimedAmount1);
        }
        
        /// <summary>
        /// Get unclaimed fees;
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        public (BigInteger, BigInteger) GetUnclaimedFees(Address from, string symbol0, string symbol1)
        {
            // Check if user has LP Token
            Runtime.Expect(UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var holder = GetLPHolder(from, symbol0, symbol1);
            return (holder.UnclaimedSymbol0, holder.UnclaimedSymbol1);
        }
        #endregion
        
        #region Utils
        /// <summary>
        /// Square root with BigInteger's
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public static BigInteger Sqrt(BigInteger n)
        {
            BigInteger root = n / 2;

            while (n < root * root)
            {
                root += n / root;
                root /= 2;
            }

            return root;
        }
        #endregion
        
        // Helpers
        // Runtime.GetBalance(symbol0, this.Address);
        // Runtime.GetBalance(symbol1, this.Address);
        // Runtime.GetBalance(symbol, this.Address);
        #endregion
        
        #endregion
    }
}
