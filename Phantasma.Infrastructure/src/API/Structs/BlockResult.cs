namespace Phantasma.Infrastructure.API;

public class BlockResult
{
    public string hash { get; set; }

    [APIDescription("Hash of previous block")]
    public string previousHash { get; set; }

    public uint timestamp { get; set; }

    // TODO support bigint here
    public uint height { get; set; }

    [APIDescription("Address of chain where the block belongs")]
    public string chainAddress { get; set; }

    [APIDescription("Network protocol version")]
    public uint protocol { get; set; }

    [APIDescription("List of transactions in block")]
    public TransactionResult[] txs { get; set; }

    [APIDescription("Address of validator who minted the block")]
    public string validatorAddress { get; set; }

    [APIDescription("Amount of KCAL rewarded by this fees in this block")]
    public string reward { get; set; }

    [APIDescription("Block events")]
    public EventResult[] events { get; set; }

    [APIDescription("Block oracles")]
    public OracleResult[] oracles { get; set; }
}