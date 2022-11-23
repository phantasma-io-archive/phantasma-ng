using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class RpcController : BaseRpcControllerV1
    {
        public class RpcRequest
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

        public class RpcResponse
        {
            public string jsonrpc { get; set; } = "2.0";
            public object result { get; set; }
            public int id { get; set; } = 1;
        }

        [APIInfo(typeof(object), "Returns query result.", false, 300)]
        [HttpPost("rpc")]
        public RpcResponse Rpc(RpcRequest req)
        {
            RpcResponse rpcResponse = null;

            try
            {
                // TODO implement something faster and more elegant
                NexusAPI.RequireNexus();
                //if (!NexusAPI.Nexus.HasGenesis()) throw new APIException("Nexus genesis is not setuped.");

                var controllers = Assembly.GetExecutingAssembly().GetTypes()
                    .Where(type => typeof(BaseControllerV1).IsAssignableFrom(type));

                foreach (var controller in controllers)
                {
                    var methodInfo = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var method in methodInfo)
                    {
                        if(method.Name.ToLower().Equals(req.method.ToLower()))
                        {
                            var methodParameters = method.GetParameters();

                            var processedParams = new List<object>();
                            var i = 0;
                            foreach (var param in req.@params)
                            {
                                if(param is JsonElement)
                                {
                                    if(methodParameters[i].ParameterType == typeof(string))
                                    {
                                        processedParams.Add(((JsonElement)param).GetString());
                                    }
                                    else if (methodParameters[i].ParameterType == typeof(Int32))
                                    {
                                        processedParams.Add(((JsonElement)param).GetInt32());
                                    }
                                    else if (methodParameters[i].ParameterType == typeof(UInt32))
                                    {
                                        processedParams.Add(((JsonElement)param).GetUInt32());
                                    }
                                    else if (methodParameters[i].ParameterType == typeof(Int64))
                                    {
                                        processedParams.Add(((JsonElement)param).GetInt64());
                                    }
                                    else if (methodParameters[i].ParameterType == typeof(UInt64))
                                    {
                                        processedParams.Add(((JsonElement)param).GetUInt64());
                                    }
                                    else if (methodParameters[i].ParameterType == typeof(Decimal))
                                    {
                                        processedParams.Add(((JsonElement)param).GetDecimal());
                                    }
                                    else if (methodParameters[i].ParameterType == typeof(bool))
                                    {
                                        processedParams.Add(((JsonElement)param).GetBoolean());
                                    }
                                    else
                                    {
                                        throw new Exception($"Unsupported parameter type {methodParameters[i].ParameterType}");
                                    }
                                }
                                else
                                {
                                    processedParams.Add(param);
                                }
                                i++;
                            }

                            var instance = controller.GetConstructors().First().Invoke(null);
                            
                            for(int j = processedParams.Count; j < methodParameters.Length; j++)
                            {
                                // We should add method's optional params as Type.Missing
                                // for reflection call to work
                                processedParams.Add(Type.Missing);
                            }

                            rpcResponse = new RpcResponse();
                            rpcResponse.result = method.Invoke(instance, processedParams.ToArray());
                            break;
                        }
                    }
                }
            }
            catch (APIException apiException)
            {
                throw;
            }
            catch (Exception e)
            {
                //Log.Error($"RPC Call error -> {e.StackTrace}");
                throw new APIException($"RPC call exception for {req}: {e.Message}");
            }

            return rpcResponse;
        }
    }
}
