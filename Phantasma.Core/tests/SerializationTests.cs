using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Phantasma.Core.Domain;
using Shouldly;

namespace Phantasma.Core.Tests;

[TestClass]
public class SerializationTests
{
    [TestMethod]
    public void serialize_test()
    {
        var serial = Substitute.For<ISerializable>();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((byte) 42);
        serial.SerializeData(writer);

        var bArray = stream.ToArray();
        bArray.ShouldBe(new byte[] {42});
    }

    [TestMethod]
    public void unserialize_test()
    {
        var serial = Substitute.For<ISerializable>();

        using var stream = new MemoryStream(new byte[] {42});
        using var reader = new BinaryReader(stream);

        serial.UnserializeData(reader);
        var readByte = stream.ReadByte();
        readByte.ShouldBe(42);
    }
}
