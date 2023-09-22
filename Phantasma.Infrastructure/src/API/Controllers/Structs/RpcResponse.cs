namespace Phantasma.Infrastructure.API.Controllers.Structs;

public class RpcResponse
{
    public string jsonrpc { get; set; } = "2.0";
    public object result { get; set; }
    public int id { get; set; } = 1;
}
