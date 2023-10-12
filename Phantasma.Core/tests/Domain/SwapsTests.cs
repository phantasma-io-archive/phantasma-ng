using System.IO;
using System.Runtime.Intrinsics.Arm;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Hashing;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract.Interop;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Core.Utils;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class SwapsTests
{
    [Fact]
    public void TestConstructor()
    {
        // Arrange
        string sourcePlatform = "source_platform";
        string sourceChain = "source_chain";
        byte[] bytes = new byte[32];
        var hash = CryptoExtensions.Sha256(bytes);
        Hash sourceHash = new Hash(bytes);
        string destinationPlatform = "destination_platform";
        string destinationChain = "destination_chain";
        byte[] bytes_dest = new byte[32];
        var hash_dest = CryptoExtensions.Sha256(bytes_dest);
        Hash destinationHash = new Hash(hash_dest);

        // Act
        ChainSwap chainSwap = new ChainSwap(sourcePlatform, sourceChain, sourceHash, destinationPlatform, destinationChain, destinationHash);

        // Assert
        Assert.Equal(sourcePlatform, chainSwap.sourcePlatform);
        Assert.Equal(sourceChain, chainSwap.sourceChain);
        Assert.Equal(sourceHash, chainSwap.sourceHash);
        Assert.Equal(destinationPlatform, chainSwap.destinationPlatform);
        Assert.Equal(destinationChain, chainSwap.destinationChain);
        Assert.Equal(destinationHash, chainSwap.destinationHash);
    }

    [Fact]
    public void TestSerializeData()
    {
        // Arrange
        string sourcePlatform = "source_platform";
        string sourceChain = "source_chain";
        byte[] bytes = new byte[32];
        var hash = CryptoExtensions.Sha256(bytes);
        Hash sourceHash = new Hash(bytes);
        string destinationPlatform = "destination_platform";
        string destinationChain = "destination_chain";
        byte[] bytes_dest = new byte[32];
        var hash_dest = CryptoExtensions.Sha256(bytes_dest);
        Hash destinationHash = new Hash(hash_dest);

        ChainSwap chainSwap = new ChainSwap(sourcePlatform, sourceChain, sourceHash, destinationPlatform,
            destinationChain, destinationHash);

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            // Act
            chainSwap.SerializeData(writer);

            // Assert
            writer.Flush();
            stream.Position = 0;
            var reader = new BinaryReader(stream);
            Assert.Equal(sourcePlatform, reader.ReadVarString());
            Assert.Equal(sourceChain, reader.ReadVarString());
            Assert.Equal(sourceHash, reader.ReadHash());
            Assert.Equal(destinationPlatform, reader.ReadVarString());
            Assert.Equal(destinationChain, reader.ReadVarString());
            Assert.Equal(destinationHash, reader.ReadHash());
        }
    }
    
    // Write a unit test for the DeserializeData method
    [Fact]
    public void TestDeserializeData()
    {
        // Arrange
        string sourcePlatform = "source_platform";
        string sourceChain = "source_chain";
        byte[] bytes = new byte[32];
        var hash = CryptoExtensions.Sha256(bytes);
        Hash sourceHash = new Hash(bytes);
        string destinationPlatform = "destination_platform";
        string destinationChain = "destination_chain";
        byte[] bytes_dest = new byte[32];
        var hash_dest = CryptoExtensions.Sha256(bytes_dest);
        Hash destinationHash = new Hash(hash_dest);

        ChainSwap chainSwap = new ChainSwap(sourcePlatform, sourceChain, sourceHash, destinationPlatform,
            destinationChain, destinationHash);

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            // Act
            chainSwap.SerializeData(writer);

            // Assert
            writer.Flush();
            stream.Position = 0;
            var reader = new BinaryReader(stream);
            var chainSwap2 = new ChainSwap();
            chainSwap2.UnserializeData(reader);
            Assert.Equal(sourcePlatform, chainSwap2.sourcePlatform);
            Assert.Equal(sourceChain, chainSwap2.sourceChain);
            Assert.Equal(sourceHash, chainSwap2.sourceHash);
            Assert.Equal(destinationPlatform, chainSwap2.destinationPlatform);
            Assert.Equal(destinationChain, chainSwap2.destinationChain);
            Assert.Equal(destinationHash, chainSwap2.destinationHash);
        }
    }
    
    
}
