using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Phantasma.Core.Domain;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class IAPIResultTests
{
    [Fact]
    public void TestFromAPIResult()
    {
        // Arrange
        var input = null as object;

        // Act
        Assert.Throws<NullReferenceException>(() => APIUtils.FromAPIResult(input));
    }
    
    [Fact]
    public void TestFromAPIResultWithData()
    {
        // Arrange
        object input = new { Name = "Test", Value = 1 };
        var expected = JsonNode.Parse(JsonSerializer.Serialize(input, input.GetType()));
        
        // Act
        var result = APIUtils.FromAPIResult(input);

        // Assert
        Assert.Equal(expected.ToString(), result.ToString());
    }
}
