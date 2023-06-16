namespace Phantasma.Infrastructure.API.Structs;

public class ReceiptResult
{
    [APIDescription("Name of nexus")]
    public string nexus { get; set; }

    [APIDescription("Name of channel")]
    public string channel { get; set; }

    [APIDescription("Index of message")]
    public string index { get; set; }

    [APIDescription("Date of message")]
    public uint timestamp { get; set; }

    [APIDescription("Sender address")]
    public string sender { get; set; }

    [APIDescription("Receiver address")]
    public string receiver { get; set; }

    [APIDescription("Script of message, in hex")]
    public string script { get; set; }
}