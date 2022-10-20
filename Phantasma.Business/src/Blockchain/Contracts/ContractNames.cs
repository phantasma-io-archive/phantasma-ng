using Phantasma.Core.Domain;

namespace Phantasma.Business.Blockchain.Contracts;

public static class ContractNames
{
    public readonly static string GasContractName = NativeContractKind.Gas.GetContractName();
    public readonly static string BlockContractName = NativeContractKind.Block.GetContractName();
    public readonly static string StakeContractName = NativeContractKind.Stake.GetContractName();
    public readonly static string SwapContractName = NativeContractKind.Swap.GetContractName();
    public readonly static string AccountContractName = NativeContractKind.Account.GetContractName();
    public readonly static string ConsensusContractName = NativeContractKind.Consensus.GetContractName();
    public readonly static string GovernanceContractName = NativeContractKind.Governance.GetContractName();
    public readonly static string StorageContractName = NativeContractKind.Storage.GetContractName();
    public readonly static string ValidatorContractName = NativeContractKind.Validator.GetContractName();
    public readonly static string InteropContractName = NativeContractKind.Interop.GetContractName();
    public readonly static string ExchangeContractName = NativeContractKind.Exchange.GetContractName();
    public readonly static string RelayContractName = NativeContractKind.Relay.GetContractName();
    public readonly static string RankingContractName = NativeContractKind.Ranking.GetContractName();
    public readonly static string MailContractName = NativeContractKind.Mail.GetContractName();
    public readonly static string MarketContractName = NativeContractKind.Market.GetContractName();

}