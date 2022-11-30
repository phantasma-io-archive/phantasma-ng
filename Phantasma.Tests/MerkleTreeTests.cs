using Xunit;
using Phantasma.Core.Cryptography;
using System;
using System.Linq;

namespace Phantasma.Core.Tests;

[Collection("MerkleTreeTests")]
public class MerkleTreeTests
{
    [Fact]
    public void TestSingleNodeMerkleSuccess()
    {
        uint fileSize = 1000;
        uint chunkSize = MerkleTree.ChunkSize;

        byte[] file = new byte[fileSize];

        Random r = new Random();
        r.NextBytes(file);

        var tree = new MerkleTree(file);

        var chunkCount = file.Length / chunkSize;
        if (chunkCount * chunkSize < file.Length)
        {
            chunkCount++;
        }

        var actualChunkSize = MerkleTree.ChunkSize < fileSize ? MerkleTree.ChunkSize : fileSize;

        var chunk = new byte[actualChunkSize];
        for (int i = 0; i < chunkCount; i++)
        {
            Array.Copy(file, i * actualChunkSize, chunk, 0, actualChunkSize);
            Assert.True(tree.VerifyContent(chunk, i));
        }
    }

    [Fact]
    public void TestSingleNodeMerkleFailure()
    {
        uint fileSize = 1000;
        uint chunkSize = MerkleTree.ChunkSize;

        byte[] file = new byte[fileSize];

        Random r = new Random();
        r.NextBytes(file);

        var tree = new MerkleTree(file);

        var chunkCount = file.Length / chunkSize;
        if (chunkCount * chunkSize < file.Length)
        {
            chunkCount++;
        }

        var actualChunkSize = MerkleTree.ChunkSize < fileSize ? MerkleTree.ChunkSize : fileSize;
        var originalChunk = new byte[actualChunkSize];

        var fakeChunk = new byte[actualChunkSize];
        r.NextBytes(fakeChunk);

        for (int i = 0; i < chunkCount; i++)
        {
            Array.Copy(file, i * actualChunkSize, originalChunk, 0, actualChunkSize);

            while (fakeChunk.SequenceEqual(originalChunk))
            {
                r.NextBytes(fakeChunk);
            }

            Assert.False(tree.VerifyContent(fakeChunk, i));
        }
    }

    [Fact]
    public void TestMultipleNodeMerkleSuccess()
    {
        uint chunkSize = MerkleTree.ChunkSize;
        uint fileSize = (uint) (chunkSize * 20.9);

        byte[] file = new byte[fileSize];

        Random r = new Random();
        r.NextBytes(file);

        var tree = new MerkleTree(file);

        var chunkCount = file.Length / chunkSize;
        if (chunkCount * chunkSize < file.Length)
        {
            chunkCount++;
        }

        for (int i = 0; i < chunkCount; i++)
        {
            var leftoverFile = fileSize - (MerkleTree.ChunkSize * i);
            var actualChunkSize = MerkleTree.ChunkSize < leftoverFile ? MerkleTree.ChunkSize : leftoverFile;
            var chunk = new byte[actualChunkSize];

            Array.Copy(file, i * MerkleTree.ChunkSize, chunk, 0, actualChunkSize);
            Assert.True(tree.VerifyContent(chunk, i));
        }
    }

    [Fact]
    public void TestMultipleNodeMerkleFailure()
    {
        uint chunkSize = MerkleTree.ChunkSize;
        uint fileSize = (uint)(chunkSize * 20.9);

        byte[] file = new byte[fileSize];

        Random r = new Random();
        r.NextBytes(file);

        var tree = new MerkleTree(file);

        var chunkCount = file.Length / chunkSize;
        if (chunkCount * chunkSize < file.Length)
        {
            chunkCount++;
        }
        
        for (int i = 0; i < chunkCount; i++)
        {
            var leftoverFile = fileSize - (MerkleTree.ChunkSize * i);
            var actualChunkSize = MerkleTree.ChunkSize < leftoverFile ? MerkleTree.ChunkSize : leftoverFile;

            var originalChunk = new byte[actualChunkSize];
            Array.Copy(file, i * MerkleTree.ChunkSize, originalChunk, 0, actualChunkSize);

            var fakeChunk = new byte[actualChunkSize];
            do
            {
                r.NextBytes(fakeChunk);
            } while (fakeChunk.SequenceEqual(originalChunk));

            Assert.False(tree.VerifyContent(fakeChunk, i));
        }
    }
}

