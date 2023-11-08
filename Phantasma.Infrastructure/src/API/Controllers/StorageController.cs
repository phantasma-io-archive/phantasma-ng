using System;
using System.Drawing;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Utils;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class StorageController : BaseControllerV1
    {
        [APIInfo(typeof(ArchiveResult), "Returns info about a specific archive.", false, 300, true)]
        [HttpGet("GetArchive")]
        public ArchiveResult GetArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            var archive = service.GetArchive(hash);
            if (archive == null)
            {
                throw new APIException("archive not found");
            }

            return service.FillArchive(archive);
        }

        [APIInfo(typeof(bool), "Writes the contents of an incomplete archive.", false, 0, true)]
        [HttpGet("WriteArchive")]
        public bool WriteArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText, [APIParameter("Block index, starting from 0", "0")] int blockIndex, [APIParameter("Block content bytes, in Base64", "QmFzZTY0IGVuY29kZWQgdGV4dA==")] string blockContent)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            var archive = service.GetArchive(hash);
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
                service.WriteArchiveBlock(archive, blockIndex, bytes);
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
            var service = ServiceUtility.GetAPIService(HttpContext);
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            var archive = service.GetArchive(hash);
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
                var bytes = service.ReadArchiveBlock(archive, blockIndex);
                return Convert.ToBase64String(bytes);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }
        }
        
        [APIInfo(typeof(IActionResult), "Reads a image from a given archive.", false, 0, false)]
        [HttpGet("ReadImage")]
        public IActionResult ReadImage([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText, string format = "png")
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            var archive = service.GetArchive(hash);
            if (archive == null)
            {
                throw new APIException("archive not found");
            }
            
            // if archive size > 4mb then it is not an image
            if (archive.Size >= 4 * 1000 * 1024)
            {
                throw new APIException("invalid image size");
            }
            
            try
            {
                var bytes = service.ReadArchiveBlock(archive, 0);
                for (int i = 1; i < archive.BlockCount; i++)
                {
                    bytes = ByteArrayUtils.ConcatBytes(bytes, service.ReadArchiveBlock(archive, i));
                }
                
                switch (format)
                {
                    case "jpg":
                    case "jpeg":
                        return File(bytes, "image/jpeg");
                    case "gif":
                        return File(bytes, "image/gif");
                    case "bmp":
                        return File(bytes, "image/bmp");
                    case "tiff":
                        return File(bytes, "image/tiff");
                    case "ico":
                        return File(bytes, "image/x-icon");
                    default:
                        return File(bytes, "image/png");
                }
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }
        }
    }
}
