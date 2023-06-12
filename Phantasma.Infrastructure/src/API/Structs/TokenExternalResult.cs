namespace Phantasma.Infrastructure.API;

public class TokenExternalResult
{
    [APIDescription("Platform name")]
    public string platform { get; set; }

    [APIDescription("External hash")]
    public string hash { get; set; }
}