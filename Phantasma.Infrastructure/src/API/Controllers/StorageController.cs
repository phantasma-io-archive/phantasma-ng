using System;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core;

namespace Phantasma.Infrastructure.Controllers
{
    public class StorageController : BaseControllerV1
    {
        [APIInfo(typeof(ArchiveResult), "Returns info about a specific archive.", false, 300, true)]
        [HttpGet("GetArchive")]
        public ArchiveResult GetArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            var nexus = NexusAPI.GetNexus();

            var archive = nexus.GetArchive(nexus.RootStorage, hash);
            if (archive == null)
            {
                throw new APIException("archive not found");
            }

            return NexusAPI.FillArchive(archive);
        }

        [APIInfo(typeof(bool), "Writes the contents of an incomplete archive.", false, 0, true)]
        [HttpGet("WriteArchive")]
        public bool WriteArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText, [APIParameter("Block index, starting from 0", "0")] int blockIndex, [APIParameter("Block content bytes, in Base64", "QmFzZTY0IGVuY29kZWQgdGV4dA==")] string blockContent)
        {
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            var nexus = NexusAPI.GetNexus();

            var archive = nexus.GetArchive(nexus.RootStorage, hash);
            if (archive == null)
            {
                throw new APIException("archive not found");
            }

            if (blockIndex < 0 || blockIndex >= archive.BlockCount)
            {
                throw new APIException("invalid block index");
            }

            var bytes = Convert.FromBase64String(blockContent);

            try
            {
                nexus.WriteArchiveBlock(archive, blockIndex, bytes);
                return true;
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }
        }

        [APIInfo(typeof(string), "Reads given archive block.", false, 0, true)]
        [HttpGet("ReadArchive")]
        public string ReadArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText, [APIParameter("Block index, starting from 0", "0")] int blockIndex)
        {
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            var nexus = NexusAPI.GetNexus();

            var archive = nexus.GetArchive(nexus.RootStorage, hash);
            if (archive == null)
            {
                throw new APIException("archive not found");
            }

            if (blockIndex < 0 || blockIndex >= archive.BlockCount)
            {
                throw new APIException("invalid block index");
            }

            try
            {
                var bytes = nexus.ReadArchiveBlock(archive, blockIndex);
                return Convert.ToBase64String(bytes);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }
        }
    }
}
