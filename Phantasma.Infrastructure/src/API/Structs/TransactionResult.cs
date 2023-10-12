namespace Phantasma.Infrastructure.API.Structs;

public class TransactionResult
{
    [APIDescription("Hash of the transaction")]
    public string hash { get; set; }

    [APIDescription("Transaction chain address")]
    public string chainAddress { get; set; }

    [APIDescription("Block time")]
    public uint timestamp { get; set; }

    [APIDescription("Block height at which the transaction was accepted")]
    public int blockHeight { get; set; }

    [APIDescription("Hash of the block")]
    public string blockHash { get; set; }

    [APIDescription("Script content of the transaction, in hexadecimal format")]
    public string script { get; set; }

    [APIDescription("Payload content of the transaction, in hexadecimal format")]
    public string payload { get; set; }

    [APIDescription("List of events that triggered in the transaction")]
    public EventResult[] events { get; set; }

    [APIDescription("Result of the transaction, if any. Serialized, in hexadecimal format")]
    public string result { get; set; }

    [APIDescription("Fee of the transaction, in KCAL, fixed point")]
    public string fee { get; set; }

    [APIDescription("Executin state of the transaction")]
    public string state { get; set; }

    [APIDescription("List of signatures that signed the transaction")]
    public SignatureResult[] signatures { get; set; }

    [APIDescription("Sender of the transaction")]
    public string sender { get; set; }

    [APIDescription("Address to pay gas from")]
    public string gasPayer { get; set; }

    [APIDescription("Address used as gas target, if any")]
    public string gasTarget { get; set; }

    [APIDescription("The txs gas price")]
    public string gasPrice { get; set; }

    [APIDescription("The txs gas limit")]
    public string gasLimit { get; set; }

    [APIDescription("Expiration time of the transaction")]
    public uint expiration { get; set; }
}