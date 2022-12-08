using System;
using System.Linq;
using System.Text.Json;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.API;

namespace Phantasma.Core.Tests.Utils;

using Phantasma.Core.Utils;
using Xunit;

public class RequestTests
{
    // write tests based on the Request class and methods
    [Fact]
    public void TestRequest()
    {
        var request = RequestUtils.RPCRequest("http://testnet.phantasma.io:5101/rpc", "GetNexus", out string myResponse );
        
        Assert.NotNull(myResponse);
        // tests
        Assert.True(request.RootElement.TryGetProperty("result", out var result));
        Assert.True(result.TryGetProperty("name", out var name));
        Assert.Equal("testnet", name.GetString());
    }
    
    [Fact]
    public void TestRequestWithParams()
    {
        var request = RequestUtils.RPCRequest("http://testnet.phantasma.io:5101/rpc", "GetBlockHeight", out string myResponse, 0, 1, "main");
        
        Assert.NotNull(myResponse);
        // tests
        Assert.True(request.RootElement.GetRawText() != null);
    }
    
    [Fact]
    public void TestRequestWithMultipleParams()
    {
        var request = RequestUtils.RPCRequest("http://testnet.phantasma.io:5101/rpc", "GetContract", out string myResponse, 0, 1, "main", "swap");
        
        Assert.NotNull(myResponse);
        // tests
        Assert.True(request.RootElement.GetRawText() != null);
        Assert.True(request.RootElement.TryGetProperty("result", out var result));
        Assert.True(result.TryGetProperty("name", out var name));
        Assert.Equal("swap", name.GetString());
    }
    
    [Fact]
    public void TestRequestBlockHeight()
    {
        var postParms = "";
        var request = RequestUtils.Request<string>(RequestType.GET, "http://testnet.phantasma.io:5101/api/v1/GetBlockHeight?chainInput=main", out string myResponse);
        Assert.True(myResponse != null);
        Assert.True(request != null);
        Assert.NotEqual(request, "1");
    }

    [Fact]
    public void TestRequestGetBlockByHeight()
    {
        var postParms = "";
        var urlRequest = "http://testnet.phantasma.io:5101/api/v1/GetBlockByHeight?chainInput=main&height=5";
        var request = RequestUtils.Request<JsonDocument>(RequestType.GET, urlRequest, out string myResponse);

        var block = new BlockResult();
        block = JsonSerializer.Deserialize<BlockResult>(myResponse);
        //var value = //BlockResult(request.RootElement);
        Assert.True(myResponse != null);
        Assert.True(request != null);
        Assert.Equal(block.hash, "92A8515F09674AB14279FC9CD0B41F3A80EE84DEAF531EFB6E3EABF9E121E0F5");
        Assert.Equal(block.timestamp, (Timestamp)1669136164);
    }

    [Fact]
    public void TestRequestAsync()
    {
        var postParms = "";
        var urlRequest = "http://testnet.phantasma.io:5101/api/v1/GetBlockByHeight?chainInput=main&height=5";
        var request = RequestUtils.RequestAsync<JsonDocument>(RequestType.GET, urlRequest);

        var block = new BlockResult();
        block = JsonSerializer.Deserialize<BlockResult>(request.Result);
        Assert.True(request != null);
        Assert.Equal(block.hash, "92A8515F09674AB14279FC9CD0B41F3A80EE84DEAF531EFB6E3EABF9E121E0F5");
        Assert.Equal(block.timestamp, (Timestamp)1669136164);
    }
    
    [Fact]
    public void TestRequestFail()
    {
        var postParms = "";
        var urlRequest = "http://testnet.phantasma.io:5101/api/v1/GetBlockByHeight?chainInput=main&height=5";
        Assert.Throws<Exception>(() => RequestUtils.Request<BlockResult>(RequestType.GET, urlRequest,out string myResponse));
    }
    
    [Fact]
    public void TestRequestAsyncFail()
    {
        var postParms = "";
        var urlRequest = "http://testnet.phantasma.io:5101/api/v1/GetBlockByHeight?chainInput=main&height=5";
        Assert.Throws<System.AggregateException>(() => RequestUtils.RequestAsync<BlockResult>(RequestType.GET, urlRequest).Result);
    }
}
