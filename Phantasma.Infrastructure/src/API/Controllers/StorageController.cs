using System;
using System.Drawing;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Utils;
using Phantasma.Infrastructure.API.Structs;

namespace Phantasma.Infrastructure.API.Controllers
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
        
        [APIInfo(typeof(IActionResult), "Reads a image from a given archive.", false, 0, false)]
        [HttpGet("ReadImage")]
        public IActionResult ReadImage([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText, string format = "png")
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
            
            // if archive size > 4mb then it is not an image
            if (archive.Size >= 4 * 1000 * 1024)
            {
                throw new APIException("invalid image size");
            }
            
            try
            {
                var bytes = nexus.ReadArchiveBlock(archive, 0);
                for (int i = 1; i < archive.BlockCount; i++)
                {
                    bytes = ByteArrayUtils.ConcatBytes(bytes, nexus.ReadArchiveBlock(archive, i));
                }
                
                if ( format == "jpg" || format == "jpeg" )
                    return File(bytes, "image/jpeg");
                else if ( format == "gif" )
                    return File(bytes, "image/gif");
                else if ( format == "bmp" )
                    return File(bytes, "image/bmp");
                else if ( format == "tiff" )
                    return File(bytes, "image/tiff");
                else if ( format == "ico" )
                    return File(bytes, "image/x-icon");
                /*else if ( format == "mp4")
                    return File(bytes, "video/mp4");
                else if ( format == "mp3")
                    return File(bytes, "audio/mpeg");*/
                
                return File(bytes, "image/png");

            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }
        }

        private Image BytesToImage(byte[] imageData)
        {
            using (MemoryStream ms = new MemoryStream(imageData))
            {
                Image image = Image.FromStream(ms);
                return image;
            }
        }
    }
}
