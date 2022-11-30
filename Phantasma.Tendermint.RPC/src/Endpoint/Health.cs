using System;
using System.Collections.Generic;
using System.Text;

namespace Tendermint.RPC.Endpoint
{
    /*
        The above command returns JSON structured like this:
        {
            "error": "",
            "result": {},
            "id": "",
            "jsonrpc": "2.0"
        }
    */

    public class ResultHealth : IEndpointResponse
    {
    }
}
