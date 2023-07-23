namespace Phantasma.Infrastructure.API;

public class ScriptResult
{
    [APIDescription("List of events that triggered in the transaction")]
    public EventResult[] events { get; set; }

    public string result { get; set; } // deprecated
        
    public string error { get; set; } // deprecated

    [APIDescription("Results of the transaction, if any. Serialized, in hexadecimal format")]
    public string[] results { get; set; }

    [APIDescription("List of oracle reads that were triggered in the transaction")]
    public OracleResult[] oracles { get; set; }
}