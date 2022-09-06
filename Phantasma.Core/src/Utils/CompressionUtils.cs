using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using K4os.Compression.LZ4;

namespace Phantasma.Core.Utils
{
    public static class CompressionUtils
    {
        public static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var dstream = new DeflateStream(output, CompressionLevel.Optimal))
            {
                dstream.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        public static byte[] Decompress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var output = new MemoryStream();
            using (var dstream = new DeflateStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }

            return output.ToArray();
        }

        /// <summary>
        /// Compresses the specified data using the LZ4 algorithm.
        /// </summary>
        /// <param name="data">The data to be compressed.</param>
        /// <returns>The compressed data.</returns>
        public static byte[] CompressLz4(this byte[] data)
        {
            int maxLength = LZ4Codec.MaximumOutputSize(data.Length);
            using var buffer = MemoryPool<byte>.Shared.Rent(maxLength);
            int length = LZ4Codec.Encode(data, buffer.Memory.Span);
            byte[] result = new byte[sizeof(uint) + length];
            BinaryPrimitives.WriteInt32LittleEndian(result, data.Length);
            buffer.Memory[..length].CopyTo(result.AsMemory(4));
            return result;
        }

        /// <summary>
        /// Decompresses the specified data using the LZ4 algorithm.
        /// </summary>
        /// <param name="data">The compressed data.</param>
        /// <param name="maxOutput">The maximum data size after decompression.</param>
        /// <returns>The original data.</returns>
        public static byte[] DecompressLz4(this byte[] data, int maxOutput)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(data);
            if (length < 0 || length > maxOutput) throw new FormatException();
            byte[] result = new byte[length];
            if (LZ4Codec.Decode(data.AsSpan(4), result) != length)
                throw new FormatException();
            return result;
        }
    }

}
