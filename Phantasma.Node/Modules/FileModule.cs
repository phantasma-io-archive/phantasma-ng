using System;
using System.IO;
using System.Linq;
using System.Threading;

using Phantasma.Business;
using Phantasma.Core;
using Phantasma.Infrastructure;
using Phantasma.Shared.Types;
using Phantasma.Spook.Command;
using Phantasma.Business.Storage;
using Serilog;

namespace Phantasma.Spook.Modules
{
    [Module("file")]
    public static class FileModule
    {
        public static void Upload(string txIdentifier, PhantasmaKeys source, string[] args)
        {
            if (args.Length != 1)
            {
                throw new CommandException("Expected args: file_path");
            }

            var filePath = args[0];

            if (!File.Exists(filePath))
            {
                throw new CommandException("File does not exist");
            }

            var nexus = NexusAPI.GetNexus();

            var fileContent = File.ReadAllBytes(filePath);
            var contentMerkle = new MerkleTree(fileContent);

            var fileName = Path.GetFileName(filePath);

            var script = ScriptUtils.BeginScript().
                AllowGas(source.Address, Address.Null, 1, 9999).
                CallContract("storage", "UploadFile", source.Address, fileName, fileContent.Length, contentMerkle, new byte[0]).
                SpendGas(source.Address).
                EndScript();
            var tx = new Transaction(nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5), txIdentifier);
            tx.Sign(source);
            var rawTx = tx.ToByteArray(true);

            var transactionController = new Infrastructure.Controllers.TransactionController();

            Log.Information($"Uploading {fileName}...");
            try
            {
                transactionController.SendRawTransaction(Base16.Encode(rawTx));
            }
            catch (Exception e)
            {
                throw new CommandException(e.Message);
            }

            Thread.Sleep(3000);
            var hash = tx.Hash.ToString();
            do
            {
                try
                {
                    var result = transactionController.GetTransaction(hash);
                }
                catch (Exception e)
                {
                    throw new CommandException(e.Message);
                }
                /*if (result is ErrorResult)
                {
                    var temp = (ErrorResult)result;
                    if (temp.error.Contains("pending"))
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        throw new CommandException(temp.error);
                    }
                }
                else*/
                {
                    break;
                }
            } while (true);

            var archiveHash = contentMerkle.Root.ToString();
            var storageController = new Infrastructure.Controllers.StorageController();
            var archive = (ArchiveResult)storageController.GetArchive(archiveHash);
            for (int i = 0; i < archive.blockCount; i++)
            {
                var ofs = (int)(i * Archive.BlockSize);
                var blockContent = fileContent.Skip(ofs).Take((int)Archive.BlockSize).ToArray();

                Log.Information($"Writing block {i+1} out of {archive.blockCount}");
                storageController.WriteArchive(archiveHash, i, Base16.Encode(blockContent));
            }

            Log.Information($"File uploaded successfully!");
        }
    }
}
