namespace Phantasma.Infrastructure.API.Structs;

public class EventResult
{
    public string address { get; set; }
    public string contract { get; set; }
    public string kind { get; set; }

    [APIDescription("Data in hexadecimal format, content depends on the event kind")]
    public string data { get; set; }
}