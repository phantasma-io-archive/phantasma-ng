namespace Phantasma.Infrastructure.API;

public class PlatformResult
{
    public string platform { get; set; }
    public string chain { get; set; }
    public string fuel { get; set; }
    public string[] tokens { get; set; }
    public InteropResult[] interop { get; set; }
}