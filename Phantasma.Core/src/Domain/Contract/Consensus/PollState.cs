namespace Phantasma.Core.Domain.Contract.Consensus;

public enum PollState
{
    Inactive,
    Active,
    Consensus,
    Failure,
    Finished
}
