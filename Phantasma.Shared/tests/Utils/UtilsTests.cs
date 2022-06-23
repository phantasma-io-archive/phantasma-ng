using System;
using System.Text.Json;
using Phantasma.Shared.Utils;
using Shouldly;
using Xunit;

namespace Phantasma.Shared.Tests.Utils;

public class UtilsTests
{
    [Fact]
    public void ToUInt32_should_return_byte_array_as_int()
    {
        // Arrange
        var bytes = new byte[] { 1, 0, 0, 0 };

        // Act
        var result = bytes.ToUInt32(0);

        // Assert
        result.ShouldBe((uint)1);
    }

    [Fact]
    public void ToJsonString_should_return_formatted_json_string()
    {
        // Arrange
        var document = JsonDocument.Parse(@"{""key"":""value""}");

        // Act
        var result = document.ToJsonString();

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldBe($@"{{{Environment.NewLine}  ""key"": ""value""{Environment.NewLine}}}");
    }
}
