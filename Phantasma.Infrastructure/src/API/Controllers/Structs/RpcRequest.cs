namespace Phantasma.Infrastructure.API.Controllers.Structs;

public struct RpcRequest
{
    public string jsonrpc { get; set; }
    public string method { get; set; }
    public string id { get; set; }
    public object[] @params { get; set; }

    public override string ToString()
    {
        return $"RPC request '{method}' with {@params.Length} params";
    }
}
