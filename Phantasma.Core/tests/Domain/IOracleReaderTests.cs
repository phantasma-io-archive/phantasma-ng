using System;
using System.Numerics;
using Moq;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Oracle;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class IOracleReaderTests
{
    [Fact]
    public void TestReadBigInteger()
    {
        // Set up the test data
        var reader = new Mock<IOracleReader>();
        var time = (Timestamp)(DateTime.UtcNow);
        var url = "price://AAPL";
        var expectedBytes = new byte[] { 0x01, 0x02, 0x03 };
        var expectedBigInt = new BigInteger(197121);

        // Set up the mock reader to return the expected bytes
        reader.Setup(x => x.Read<byte[]>(time, url)).Returns(expectedBytes);

        // Call the method under test
        var result = reader.Object.ReadBigInteger(time, url);

        // Verify that the method under test returns the expected result
        Assert.Equal(expectedBigInt, result);
    }

    [Fact]
    public void TestReadBigInteger_WithEmptyByteArray()
    {
        // Set up the test data
        var reader = new Mock<IOracleReader>();
        var time = (Timestamp)(DateTime.UtcNow);
        var url = "price://AAPL";
        var expectedBytes = new byte[0];

        // Set up the mock reader to return the expected bytes
        reader.Setup(x => x.Read<byte[]>(time, url)).Returns(expectedBytes);

        // Call the method under test
        var result = reader.Object.ReadBigInteger(time, url);

        // Verify that the method under test returns the expected result
        Assert.Equal(-1, result);
    }
    [Fact]
    public void TestReadBigInteger_WithNullByteArray()
    {
        // Set up the test data
        var reader = new Mock<IOracleReader>();
        var time = (Timestamp)(DateTime.UtcNow);
        var url = "price://AAPL";
        byte[] expectedBytes = null;

        // Set up the mock reader to return the expected bytes
        reader.Setup(x => x.Read<byte[]>(time, url)).Returns(expectedBytes);

        // Call the method under test
        var result = reader.Object.ReadBigInteger(time, url);

        // Verify that the method under test returns the expected result
        Assert.Equal(-1, result);
    }

    [Fact]
    public void TestReadPrice()
    {
        // Set up the test data
        var reader = new Mock<IOracleReader>();
        var time = (Timestamp)(DateTime.UtcNow);
        var symbol = "AAPL";
        var expectedUrl = "price://AAPL";
        var expectedBytes = new byte[] { 0x01, 0x02, 0x03 };
        var expectedBigInt = new BigInteger(expectedBytes, true);

        // Set up the mock reader to return the expected bytes
        reader.Setup(x => x.Read<byte[]>(time, expectedUrl)).Returns(expectedBytes);

        // Call the method under test
        var result = reader.Object.ReadPrice(time, symbol);

        // Verify that
        // 1. the method under test returns the expected result
        // 2. the mock reader was called with the expected URL
        Assert.Equal(expectedBigInt, result);
        reader.Verify(x => x.Read<byte[]>(time, expectedUrl), Times.Once);
    }

}
