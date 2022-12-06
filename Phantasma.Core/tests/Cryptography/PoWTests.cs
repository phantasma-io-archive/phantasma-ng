namespace Phantasma.Core.Tests.Cryptography;

using Xunit;

using Phantasma.Core.Cryptography;

public class PoWTests
{
    
    public Hash AddDificulty(Hash hash, int targetDifficulty)
    {
        var Payload = new byte[4];
        if (targetDifficulty == 0)
        {
            return Hash.Null; // no mining necessary 
        }

        uint nonce = 0;

        while (true)
        {
            if (hash.GetDifficulty() >= targetDifficulty)
            {
                return hash;
            }

            if (nonce == 0)
            {
                Payload = new byte[4];
            }

            nonce++;
            if (nonce == 0)
            {
                return Hash.Null;
            }

            Payload[0] = (byte)((nonce >> 0) & 0xFF);
            Payload[1] = (byte)((nonce >> 8) & 0xFF);
            Payload[2] = (byte)((nonce >> 16) & 0xFF);
            Payload[3] = (byte)((nonce >> 24) & 0xFF);
            
            var sha256 = CryptoExtensions.Sha256(hash.ToByteArray());
            hash = new Hash(sha256);
        }
    }
    
    [Fact]
    public void TestPoW()
    {
        var hash = Hash.Parse("0000000000000000000000000000000000000000000000000000000000000000");

        var nonce = PoWUtils.GetDifficulty(hash);

        Assert.True(nonce > 0);
    }
    
    [Fact]
    public void TestPoWNone()
    {
        var data = new byte[] {0x01, 0x10, 0x13, 0x13};
        var sha256 = CryptoExtensions.Sha256(data);
        var hash = new Hash(sha256);

        var nonce = PoWUtils.GetDifficulty(hash);

        Assert.Equal(nonce, 0);
    }
    
    [Fact]
    public void TestPoWMinimal()
    {
        var data = new byte[] {0x01, 0x10, 0x13, 0x13};
        var sha256 = CryptoExtensions.Sha256(data);
        var hash = new Hash(sha256);
        hash = AddDificulty(hash, 5);

        var nonce = PoWUtils.GetDifficulty(hash);

        Assert.Equal((int)ProofOfWork.Minimal, nonce);
    }
    
    [Fact]
    public void TestPoWModerate()
    {
        var data = new byte[] {0x01, 0x10, 0x13, 0x13, 0x13};
        var sha256 = CryptoExtensions.Sha256(data);
        var hash = new Hash(sha256);
        hash = AddDificulty(hash, 15);

        var nonce = PoWUtils.GetDifficulty(hash);

        Assert.Equal((int)ProofOfWork.Moderate, nonce);
    }
    
    [Fact]
    public void TestPoWHard()
    {
        var data = new byte[] {0x01, 0x10, 0x13, 0x13, 0x13};
        var sha256 = CryptoExtensions.Sha256(data);
        var hash = new Hash(sha256);
        hash = AddDificulty(hash, 19);

        var nonce = PoWUtils.GetDifficulty(hash);

        Assert.Equal((int)ProofOfWork.Hard, nonce);
    }
    
    [Fact]
    public void TestPoWHeavy()
    {
        var data = new byte[] {0x01, 0x10, 0x13, 0x13, 0x13};
        var sha256 = CryptoExtensions.Sha256(data);
        var hash = new Hash(sha256);
        hash = AddDificulty(hash, 24);

        var nonce = PoWUtils.GetDifficulty(hash);

        Assert.Equal((int)ProofOfWork.Heavy, nonce);
    }

    [Fact(Skip="It takes to long")]
    public void TestPoWExtreme()
    {
        var data = new byte[] {0x01, 0x10, 0x13, 0x13, 0x13, 0x13};
        var sha256 = CryptoExtensions.Sha256(data);
        var hash = new Hash(sha256);
        hash = AddDificulty(hash, 30);

        var nonce = PoWUtils.GetDifficulty(hash);

        Assert.Equal((int)ProofOfWork.Extreme, nonce);
    }
}
