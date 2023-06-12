using System.Collections.Generic;
using System.Net;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract.Validator;
using RestSharp;
using Serilog;
using Tendermint.RPC;
using Tendermint.RPC.Endpoint;

namespace Phantasma.Node;

public class NodeConnector
{
    private readonly Dictionary<string, IRestClient> restClients;
    private readonly List<ValidatorSettings> validators;
    
    public NodeConnector(List<ValidatorSettings> validators)
    {
        this.restClients = new Dictionary<string, IRestClient>();
        this.validators = validators;
        SetupRestClients();
    }

    private void SetupRestClients()
    {
        foreach (var validator in validators)
        {
            var restClient = new RestClient(validator.URL).UseSerializer(() => new JsonNetSerializer());
            restClients.Add(validator.Address.Text, restClient);
        }
    }
    
    private T Execute<T>(RestRequest request, IRestClient restClient) where T : IEndpointResponse, new()
    {
        request.RequestFormat = DataFormat.Json;
        var response = restClient.Execute<T>(request);
        Log.Information("Request -> " + request.Resource);
        Log.Information("Request -> " + request.Parameters);
        if (response.StatusCode != HttpStatusCode.OK || response.ErrorException != null)
        {
            string message = $"RestClient response error StatusCode: {(int)response.StatusCode} {response.StatusCode}";
            if (response.ErrorException != null)
            {
                message += $" ErrorException: {response.ErrorException}";
            }
            throw new NodeRpcException(message);
        }
        if (response.ErrorMessage != null)
        {
            throw new NodeRpcException("Error received code: " + response.StatusCode + " message: " + response.ErrorMessage + " data: " + response.Data);
        }
        return response.Data;
    }
    
    public ResultHealth Health(Address address)
    {
        var request = new RestRequest("/api/v1/health", Method.GET);
        var response = Execute<ResultHealth>(request, restClients[address.Text]);
        return response;
    }
    
    public ResultStatus Status(Address address)
    {
        var request = new RestRequest("/api/v1/status", Method.GET);
        var response = Execute<ResultStatus>(request, restClients[address.Text]);
        return response;
    }
    
    public ResultNetInfo NetInfo(Address address)
    {
        var request = new RestRequest("/api/v1/net_info", Method.GET);
        var response = Execute<ResultNetInfo>(request, restClients[address.Text]);
        return response;
    }
    
    public ResponseQuery RequestBlockHeightFromAddress(Address address, int height)
    {
        if (!restClients.ContainsKey(address.Text)) return new ResponseQuery();
        try
        {
            var request = new RestRequest("/api/v1/request_block", Method.GET);
            request.AddQueryParameter("height", height.ToString());
            var response = this.Execute<ResultAbciQuery>(request, restClients[address.Text]);
            return response.Response;
        }catch (NodeRpcException e)
        {
            Log.Error(e.Message);
            return new ResponseQuery();
        }
        
        return new ResponseQuery();
    }
    
    public ResultAbciQuery AbciQuery(Address address, string path, string data, int height = 0, bool prove = false)
    {
        if (!restClients.ContainsKey(address.Text)) return new ResultAbciQuery();
        var request = new RestRequest("/api/v1/abci_query", Method.GET);
        request.AddQueryParameter("path", path);
        request.AddQueryParameter("data", data);
        request.AddQueryParameter("height", height.ToString());
        request.AddQueryParameter("prove", prove.ToString());
        var response = this.Execute<ResultAbciQuery>(request, restClients[address.Text]);
        return response;
    }
    
    /*public List<ValidatorSettings> GetValidatorsSettings(Address address)
    {
        if (!restClients.ContainsKey(address.Text)) return new List<ValidatorSettings>();
        var request = new RestRequest("/api/v1/request_block", Method.GET);
        var response = restClients[address.Text].Execute<ValidatorSettings[]>(request);
        return response.Response;
    }*/
    
    
    
    
}
