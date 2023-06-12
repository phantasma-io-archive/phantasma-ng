namespace Phantasma.Core.Domain.Contract.Interop;

public enum CrossChainTransferStatus
{
    Pending,
    InProgress,
    Confirmed,
    Expired,
    Failed,
    Cancelled
}
