using System.IO;
using System.Numerics;
using System.Collections.Generic;
using Phantasma.Core;

namespace Phantasma.Business;

public class SerializedBlockList {

    public Dictionary<BigInteger, Block> Blocks { get; set; }
    public Dictionary<BigInteger,List<Transaction>> BlockTransactions { get; set; }


    public SerializedBlockList() {
        this.Blocks = new();
        this.BlockTransactions = new();
    }

    public byte[] ToByteArray()
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                Serialize(writer);
            }

            return stream.ToArray();
        }
    }

    internal void Serialize(BinaryWriter writer)
    {
        writer.WriteVarInt(this.Blocks.Count);
        //Console.WriteLine("================================ start +++++++++++++++++++++++++++++++++++++++++++++++");
        //Console.WriteLine($"DEBUG: serialize {this.Blocks.Count} blocks");
        foreach (var pair in this.Blocks)
        {
            var height = pair.Key;
            var block = pair.Value;
            Console.WriteLine($"DEBUG: serialize {block.Height}");

            writer.WriteByteArray(block.ToByteArray(true));

            var txs = this.BlockTransactions[height];
            Console.WriteLine($"DEBUG: serialize {txs.Count} transactions in block {block.Height}");
            writer.WriteVarInt(txs.Count);
            foreach (var tx in txs)
            {
                writer.WriteByteArray(tx.ToByteArray(true));
            }
        }
        //Console.WriteLine("================================ end +++++++++++++++++++++++++++++++++++++++++++++++");
    }

    public static SerializedBlockList Deserialize(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        {
            using (var reader = new BinaryReader(stream))
            {
                return Deserialize(reader);
            }
        }
    }

    public static SerializedBlockList Deserialize(BinaryReader reader)
    {
        var sbl = new SerializedBlockList();
        sbl.DeserializeData(reader);
        return sbl;
    }

    public void DeserializeData(BinaryReader reader)
    {
        var blockCount = (int)reader.ReadVarInt();
        for (int i = 0; i < blockCount; i++)
        {
            var serializedBlock = reader.ReadByteArray();
            var block = Block.Unserialize(serializedBlock);
            this.Blocks[i] = block;

            var txCount = (int)reader.ReadVarInt();
            List<Transaction> txs = new ();
            for (int j = 0; j < txCount; j++)
            {
                var serializedTx = reader.ReadByteArray();
                var tx = Transaction.Unserialize(serializedTx);
                txs.Add(tx);
            }
            this.BlockTransactions[block.Height] = txs;
        }

        //this.NexusName = reader.ReadVarString();
        //this.ChainName = reader.ReadVarString();
        //this.Script = reader.ReadByteArray();
        //this.Expiration = reader.ReadUInt32();
        //this.Payload = reader.ReadByteArray();

        //// check if we have some signatures attached
        //try
        //{
        //    var signatureCount = (int)reader.ReadVarInt();
        //    this.Signatures = new Signature[signatureCount];
        //    for (int i = 0; i < signatureCount; i++)
        //    {
        //        Signatures[i] = reader.ReadSignature();
        //    }
        //}
        //catch
        //{
        //    this.Signatures = new Signature[0];
        //}

        //this.UpdateHash();
    }
}
