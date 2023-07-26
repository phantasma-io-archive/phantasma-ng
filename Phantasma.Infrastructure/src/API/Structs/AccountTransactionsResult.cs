namespace Phantasma.Infrastructure.API.Structs;

public class AccountTransactionsResult
{
    public string address { get; set; }

    [APIDescription("List of transactions")]
    public TransactionResult[] txs { get; set; }
}