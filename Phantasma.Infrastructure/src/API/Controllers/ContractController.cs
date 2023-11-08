using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Cryptography;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class ContractController : BaseControllerV1
    {
        [APIInfo(typeof(ContractResult), "Returns the Contracts on the chain.", false, 300)]
        [HttpGet("GetContracts")]
        public ContractResult[] GetContracts(
            [APIParameter("Chain address or name where the contract is deployed", "main")]
            string chainAddressOrName,
            [APIParameter(description: "Extended data. Includes scripts, methods, and event. (deprecated, will be removed in future versions)", value: "true")]
            bool extended = true)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var chain = service.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            var contracts = chain.GetContracts(chain.Storage);
            return contracts.Select(contract => service.FillContract(contract.Name,
                chain.GetContractByName(chain.Storage, contract.Name), extended)).ToArray();
        }

        [APIInfo(typeof(ContractResult), "Returns the ABI interface of specific contract.", false, 300)]
        [HttpGet("GetContract")]
        public ContractResult GetContract(
            [APIParameter("Chain address or name where the contract is deployed", "main")] string chainAddressOrName, 
            [APIParameter("Contract name", "account")] string contractName)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var chain = service.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (string.IsNullOrEmpty(contractName))
            {
                throw new APIException("Invalid contract name");
            }

            if (!chain.IsContractDeployed(chain.Storage, contractName))
            {
                throw new APIException("Contract not found");
            }

            var contract = chain.GetContractByName(chain.Storage, contractName);
            return service.FillContract(contractName, contract, true);
        }

        [APIInfo(typeof(ContractResult), "Returns the ABI interface of specific contract.", false, 300)]
        [HttpGet("GetContractByAddress")]
        public ContractResult GetContractByAddress(
            [APIParameter("Chain address or name where the contract is deployed", "main")]
            string chainAddressOrName,
            [APIParameter("Contract address", "account")]
            string contractAddress)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var chain = service.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (!Address.IsValidAddress(contractAddress))
            {
                throw new APIException("Invalid contract address");
            }

            var contractAddressObj = Address.FromText(contractAddress);

            if (contractAddressObj.IsNull)
            {
                throw new APIException("Invalid contract address");
            }

            if (!chain.IsContractDeployed(chain.Storage, contractAddressObj))
            {
                throw new APIException("Contract not found");
            }

            var contract = chain.GetContractByAddress(chain.Storage, contractAddressObj);
            return service.FillContract(contractAddress, contract, true);
        }
    }
}
