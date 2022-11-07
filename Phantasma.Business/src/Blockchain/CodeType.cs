namespace Phantasma.Business.Blockchain
{
    public enum CodeType : uint
    {
        Ok = 0,
        Error = 1,
        Expired = 2,
        InvalidChain = 3,
        UnsignedTx = 4,
        NotSignedBySender = 5,
        MissingFuel = 6,
        UnsupportedVersion = 7,
        NoUserAddress = 8,
        NoSystemAddress = 9,
        GasFeeTooLow = 10,
        InvalidScript = 11
    }
}
