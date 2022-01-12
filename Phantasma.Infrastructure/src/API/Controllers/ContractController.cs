using Microsoft.AspNetCore.Mvc;

namespace Phantasma.Infrastructure.Controllers
{
    public class ContractController : BaseControllerV1
    {
        [APIInfo(typeof(ContractResult), "Returns the ABI interface of specific contract.", false, 300)]
        [HttpGet("GetContract")]
        public ContractResult GetContract([APIParameter("Chain address or name where the contract is deployed", "main")] string chainAddressOrName, [APIParameter("Contract name", "account")] string contractName)
        {
            var chain = NexusAPI.FindChainByInput(chainAddressOrName);
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
            return NexusAPI.FillContract(contractName, contract);
        }
    }
}
