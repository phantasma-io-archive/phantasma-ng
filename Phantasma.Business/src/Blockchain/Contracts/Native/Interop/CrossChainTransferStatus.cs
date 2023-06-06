namespace Phantasma.Business.Blockchain.Contracts.Native;

public enum CrossChainTransferStatus
{
    Pending,
    InProgress,
    Confirmed,
    Expired,
    Failed,
    Cancelled
}
