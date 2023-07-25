using System.Linq;
using System.Numerics;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Numerics;
using Phantasma.Infrastructure.API.Structs;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class BlockController : BaseControllerV1
    {
        [APIInfo(typeof(int), "Returns the height of a chain.", false, 3)]
        [APIFailCase("chain is invalid", "4533")]
        [HttpGet("GetBlockHeight")]
        public string GetBlockHeight([APIParameter("Address or name of chain", "root")] string chainInput)
        {
            var chain = NexusAPI.FindChainByInput(chainInput);

            if (chain == null)
            {
                //throw new APIException("invalid chain");
                return "invalid chain";
            }

            return chain.Height.ToString();
        }

        [APIInfo(typeof(int), "Returns the number of transactions of given block hash or error if given hash is invalid or is not found.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [HttpGet("GetBlockTransactionCountByHash")]
        public int GetBlockTransactionCountByHash([APIParameter("Chain address or name where the market is located", "main")] string chainAddressOrName, [APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
        {
            var chain = NexusAPI.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }


            if (Hash.TryParse(blockHash, out var hash))
            {
                var block = chain.GetBlockByHash(hash);

                if (block != null)
                {
                    int count = block.TransactionHashes.Count();

                    return count;
                }
            }

            throw new APIException("invalid block hash");
        }

        [APIInfo(typeof(BlockResult), "Returns information about a block by hash.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [HttpGet("GetBlockByHash")]
        public BlockResult GetBlockByHash([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var nexus = NexusAPI.GetNexus();

                var chains = nexus.GetChains(nexus.RootStorage);
                foreach (var chainName in chains)
                {
                    var chain = nexus.GetChainByName(chainName);
                    var block = chain.GetBlockByHash(hash);
                    if (block != null)
                    {
                        return NexusAPI.FillBlock(block, chain);
                    }
                }
            }

            throw new APIException("invalid block hash");
        }

        [APIInfo(typeof(string), "Returns a serialized string, containing information about a block by hash.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [HttpGet("GetRawBlockByHash")]
        public string GetRawBlockByHash([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var nexus = NexusAPI.GetNexus();

                var chains = nexus.GetChains(nexus.RootStorage);
                foreach (var chainName in chains)
                {
                    var chain = nexus.GetChainByName(chainName);
                    var block = chain.GetBlockByHash(hash);
                    if (block != null)
                    {
                        return block.ToByteArray(true).Encode();
                    }
                }
            }

            throw new APIException("invalid block hash");
        }

        [APIInfo(typeof(BlockResult), "Returns information about a block by height and chain.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
        [HttpGet("GetBlockByHeight")]
        public BlockResult GetBlockByHeight([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput, [APIParameter("Height of block", "1")] string height)
        {
            var chain = NexusAPI.FindChainByInput(chainInput);

            if (chain == null)
            {
                throw new APIException("chain not found");
            }
            
            if (!BigInteger.TryParse(height, out var parsedHeight))
            {
                throw new APIException("invalid number");
            }
            var blockHash = chain.GetBlockHashAtHeight(parsedHeight);
            var block = chain.GetBlockByHash(blockHash);

            if (block != null)
            {
                return NexusAPI.FillBlock(block, chain);
            }

            throw new APIException("block not found");
        }

        [APIInfo(typeof(string), "Returns a serialized string, in hex format, containing information about a block by height and chain.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
        [HttpGet("GetRawBlockByHeight")]
        public string GetRawBlockByHeight([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput, [APIParameter("Height of block", "1")] string height)
        {
            var nexus = NexusAPI.GetNexus();

            var chain = nexus.GetChainByName(chainInput);

            if (chain == null)
            {
                if (!Address.IsValidAddress(chainInput))
                {
                    throw new APIException("chain not found");
                }
                chain = nexus.GetChainByAddress(Address.FromText(chainInput));
            }

            if (chain == null)
            {
                throw new APIException("chain not found");
            }

            if (!BigInteger.TryParse(height, out var parsedHeight))
            {
                throw new APIException("invalid number");
            }
            var blockHash = chain.GetBlockHashAtHeight(parsedHeight);
            var block = chain.GetBlockByHash(blockHash);

            if (block != null)
            {
                return block.ToByteArray(true).Encode();
            }

            throw new APIException("block not found");
        }
        
        [APIInfo(typeof(BlockResult), "Returns information about the latest block.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
        [HttpGet("GetLatestBlock")]
        public BlockResult GetLatestBlock([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput)
        {
            var chain = NexusAPI.FindChainByInput(chainInput);

            if (chain == null)
            {
                throw new APIException("chain not found");
            }

            var blockHash = chain.GetBlockHashAtHeight(chain.Height);
            var block = chain.GetBlockByHash(blockHash);

            if (block != null)
            {
                return NexusAPI.FillBlock(block, chain);
            }

            throw new APIException("block not found");
        }
        
        [APIInfo(typeof(string), "Returns a serialized string, in hex format, containing information about the latest block.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
        [HttpGet("GetRawLatestBlock")]
        public string GetRawLatestBlock([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput)
        {
            var chain = NexusAPI.FindChainByInput(chainInput);

            if (chain == null)
            {
                throw new APIException("chain not found");
            }

            var blockHash = chain.GetBlockHashAtHeight(chain.Height);
            var block = chain.GetBlockByHash(blockHash);

            if (block != null)
            {
                return block.ToByteArray(true).Encode();
            }

            throw new APIException("block not found");
        }
    }
}
