namespace Types
{
    // Extracted from js-abci
    public enum CodeType : uint
    {
        Ok = 0,
        Unauthorized = 1,
        NoPayload = 2,
        UnknownCallNumber = 3,
        TransferAlreadyStarted = 4,
        NoTransferInitiated = 5,
        UnknownNewOwner = 6,
        PhoneNumberAlreadyExists = 7
    }
}
