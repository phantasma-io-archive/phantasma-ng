using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class ContractController : BaseControllerV1
    {
        
        [APIInfo(typeof(ContractResult), "Returns the Contracts on the chain.", false, 300)]
        [HttpGet("GetContracts")]
        public ContractResult[] GetContracts([APIParameter("Chain address or name where the contract is deployed", "main")] string chainAddressOrName)
        {
            var chain = NexusAPI.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            var contracts =  chain.GetContracts(chain.Storage);
            return contracts.Select(contract => NexusAPI.FillContract(contract.Name, 
                chain.GetContractByName(chain.Storage, contract.Name))).ToArray();
        }

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
        
        [APIInfo(typeof(ContractResult), "Returns the ABI interface of specific contract.", false, 300)]
        [HttpGet("GetContractByAddress")]
        public ContractResult GetContractByAddress([APIParameter("Chain address or name where the contract is deployed", "main")] string chainAddressOrName, [APIParameter("Contract address", "account")] string contractAddress)
        {
            var chain = NexusAPI.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (string.IsNullOrEmpty(contractAddress))
            {
                throw new APIException("Invalid contract address");
            }

            if (!chain.IsContractDeployed(chain.Storage, contractAddress))
            {
                throw new APIException("Contract not found");
            }
            
            if ( !Address.IsValidAddress(contractAddress) )
            {
                throw new APIException("Invalid contract address");
            }
            
            var contractAddressObj = Address.FromText(contractAddress);
            var contract = chain.GetContractByAddress(chain.Storage, contractAddressObj);
            return NexusAPI.FillContract(contractAddress, contract);
        }
    }
}
