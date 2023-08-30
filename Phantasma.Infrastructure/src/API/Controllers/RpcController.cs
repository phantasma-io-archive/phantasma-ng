using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Infrastructure.API.Controllers.Structs;
using Serilog;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class RpcController : BaseRpcControllerV1
    {
        private static Dictionary<string, MethodInfo> methodCache = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

        [APIInfo(typeof(object), "Returns query result.", false, 300)]
        [HttpPost("rpc")]
        public RpcResponse Rpc(RpcRequest req)
        {
            try
            {
                NexusAPI.RequireNexus();
                var rpcResponse = GetControllerResponse(req);

                // Set the default properties here
                rpcResponse.jsonrpc = "2.0";

                // Convert the RpcRequest's id to an int and set it to the RpcResponse's id
                if (int.TryParse(req.id, out int requestId))
                {
                    rpcResponse.id = requestId;
                }
                else
                {
                    Log.Warning($"Invalid ID format in RpcRequest: {req.id}. Defaulting RpcResponse ID to 1.");
                    rpcResponse.id = 1;
                }

                return rpcResponse;
            }
            catch (TargetInvocationException tie) when (tie.InnerException is APIException apiException)
            {
                Log.Error($"API - RPC Call error due to invoked method -> {apiException.Message}");
                throw new APIException($"RPC call exception for {req}: {apiException.Message}.");;
            }
            catch (APIException apiException)
            {
                Log.Error($"API - RPC Call error -> {apiException.Message}");
                // Depending on how your API handles errors, you may want to return a structured error response instead of throwing.
                //throw new APIException($"An error occurred while processing the request. {apiException.Message}");
                throw apiException;
            }
            catch (Exception e)
            {
                Log.Error($"RPC Call error -> {e.StackTrace}");
                // General exceptions should have a more generic error message returned to the client.
                throw new APIException($"RPC call exception for {req}: {e.Message}.");
            }
        }
        
        /// <summary>
        /// Get the response from the controller method
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private RpcResponse GetControllerResponse(RpcRequest req)
        {
            InitializeMethodCache();
            
            if(!methodCache.TryGetValue(req.method, out var targetMethod))
            {
                throw new Exception($"Method {req.method} not found.");
            }
            
            var processedParams = ProcessParameters(req.@params, targetMethod.GetParameters());
            
            var controllerInstance = Activator.CreateInstance(targetMethod.DeclaringType);
            
            var result = targetMethod.Invoke(controllerInstance, processedParams.ToArray());
            
            return new RpcResponse { result = result };
        }

        /// <summary>
        /// Initialize the method cache
        /// </summary>
        private void InitializeMethodCache()
        {
            if(methodCache.Any())
            {
                return; // Already initialized
            }

            var controllers = Assembly.GetExecutingAssembly().GetTypes()
                            .Where(type => typeof(BaseControllerV1).IsAssignableFrom(type));
            
            foreach(var controller in controllers)
            {
                foreach(var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    methodCache[method.Name] = method;
                }
            }
        }

        /// <summary>
        /// Process the parameters
        /// </summary>
        /// <param name="requestParams"></param>
        /// <param name="methodParameters"></param>
        /// <returns></returns>
        private List<object> ProcessParameters(object[] requestParams, ParameterInfo[] methodParameters)
        {
            var processedParams = new List<object>();

            for(int i = 0; i < requestParams.Length; i++)
            {
                var param = requestParams[i];

                if(param is JsonElement element)
                {
                    processedParams.Add(ConvertJsonElement(element, methodParameters[i].ParameterType));
                }
                else
                {
                    processedParams.Add(param);
                }
            }

            for(int j = processedParams.Count; j < methodParameters.Length; j++)
            {
                processedParams.Add(Type.Missing);
            }

            return processedParams;
        }

        /// <summary>
        /// Convert the JsonElement to the target type
        /// </summary>
        /// <param name="element"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private object ConvertJsonElement(JsonElement element, Type targetType)
        {
            var converters = new Dictionary<Type, Func<JsonElement, object>>
            {
                { typeof(string), e => e.GetString() },
                { typeof(int), e => e.GetInt32() },
                { typeof(uint), e => e.GetUInt32() },
                { typeof(long), e => e.GetInt64() },
                { typeof(ulong), e => e.GetUInt64() },
                { typeof(decimal), e => e.GetDecimal() },
                { typeof(bool), e => e.GetBoolean() },
                { typeof(byte[]), e => Convert.FromBase64String(e.GetString())},
                { 
                    typeof(BigInteger), e =>
                    {
                        var value = e.GetString();
                        if(!BigInteger.TryParse(value, out var result))
                        {
                            throw new Exception($"Invalid BigInteger {value}");
                        }

                        return result;
                    }
                },
                {
                    typeof(Address), e =>
                    {
                        var address = e.GetString();
                        if(!Address.IsValidAddress(address))
                        {
                            throw new Exception($"Invalid address {address}");
                        }

                        return Address.FromText(address);
                    }
                },
                {
                    typeof(Hash), e =>
                    {
                        var hash = e.GetString();
                        if(!Hash.TryParse(hash, out var result))
                        {
                            throw new Exception($"Invalid hash {hash}");
                        }

                        return result;
                    }
                }
            };

            if(!converters.TryGetValue(targetType, out var converter))
            {
                throw new Exception($"Unsupported parameter type {targetType}");
            }

            return converter(element);
        }
    }
}
