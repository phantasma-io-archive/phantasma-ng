using System.Numerics;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract.Market.Enums;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Core.Domain.Contract.Market.Structs;

public struct MarketAuction
{
    public readonly Address Creator;
    public readonly Timestamp StartDate;
    public readonly Timestamp EndDate;
    public readonly string BaseSymbol;
    public readonly string QuoteSymbol;
    public readonly BigInteger TokenID;
    public readonly BigInteger Price;
    public readonly BigInteger EndPrice;
    public readonly BigInteger ExtensionPeriod;
    public readonly TypeAuction Type;
    public readonly BigInteger ListingFee;
    public readonly Address ListingFeeAddress;
    public readonly BigInteger BuyingFee;
    public readonly Address BuyingFeeAddress;
    public readonly Address CurrentBidWinner;
    
    public MarketAuction(Address creator, Timestamp startDate, Timestamp endDate, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, BigInteger endPrice, BigInteger extensionPeriod, TypeAuction typeAuction, BigInteger listingFee, Address listingFeeAddress, BigInteger buyingFee, Address buyingFeeAddress, Address currentBidWinner)
    {
        Creator = creator;
        StartDate = startDate;
        EndDate = endDate;
        BaseSymbol = baseSymbol;
        QuoteSymbol = quoteSymbol;
        TokenID = tokenID;
        Price = price;
        EndPrice = endPrice;
        ExtensionPeriod = extensionPeriod;
        Type = typeAuction;
        ListingFee = listingFee;
        ListingFeeAddress = listingFeeAddress;
        BuyingFee = buyingFee;
        BuyingFeeAddress = buyingFeeAddress;
        CurrentBidWinner = currentBidWinner;
    }
}
