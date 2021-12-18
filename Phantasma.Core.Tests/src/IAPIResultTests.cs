using Phantasma.Core;
using Shouldly;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;
using static Phantasma.Core.WalletLink;

namespace GhostDevs.MP.Tests
{

    public class apiResultSerializationTests
    {
        [Fact]
        public void api_result_serialization_works_correctly()
        {
            // Arrange
            var authorization = new Authorization() { wallet = "WALLETNAME", nexus = "NEXUSNAME", dapp = "DAPP", token = "TOKEN", version = 10 };
            var account = new Account() { address = "P2KJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJj", alias = "justauser", avatar = "todoputavatarhere", external = null, name = "ACCOUNTNAME", platform = "PLATFORM", balances = new Balance[] { new Balance() { symbol = "SOUL", decimals = 8, value = "100000000000" }, new Balance() { symbol = "KCAL", decimals = 10, value = "10000000000000" } }, files = new File[] { new File() { name = "TestFile1", date = 1638030670, hash = "0xAAAA", size = 1024 }, new File() { name = "TestFile2", date = 1638030770, hash = "0xBBBB", size = 1000000 } } };

            var apiResults = new JsonNode[] {
                APIUtils.FromAPIResult(new Error() { message = "Malformed request" }),
                APIUtils.FromAPIResult("Single result string value"),
                // Old: APIUtils.FromAPIResult(new SingleResult() { value = "Single result string value" }),
                APIUtils.FromAPIResult(46),
                // Old: APIUtils.FromAPIResult(new SingleResult() { value = 46 }),
                APIUtils.FromAPIResult(authorization),
                // Old: APIUtils.FromAPIResult(new SingleResult() { value = authorization }),
                APIUtils.FromAPIResult(account),
                // Old: APIUtils.FromAPIResult(new SingleResult() { value = account }),
                APIUtils.FromAPIResult(new string[]{ "value1", "value2", "value3" }),
                // Old: APIUtils.FromAPIResult(new ArrayResult() { values = new string[]{ "value1", "value2", "value3" } }),
                APIUtils.FromAPIResult(new object[]{ "value1", "value2", "value3" }),
                // Old: APIUtils.FromAPIResult(new ArrayResult() { values = new object[]{ "value1", "value2", "value3" } }),
                APIUtils.FromAPIResult(new object[]{ 1, "value2", 3 }),
                // Old: APIUtils.FromAPIResult(new ArrayResult() { values = new object[]{ 1, "value2", 3 } }),
                APIUtils.FromAPIResult(authorization),
                APIUtils.FromAPIResult(account),
                APIUtils.FromAPIResult(new object[]{ authorization, account, 3 })
                // Old: APIUtils.FromAPIResult(new ArrayResult() { values = new object[]{ authorization, account, 3 } })
            };

            // Act
            var serializedResultsList = new List<string>();
            for(var i = 0; i < apiResults.Length; i++)
            {
                serializedResultsList.Add(apiResults[i].ToJsonString());
            }
            var serializedResults = serializedResultsList.ToArray();

            // Assert
            serializedResults[0].ShouldBe(@"{""message"":""Malformed request""}");
            // Lunar: serializedResults[0].ShouldBe(@"{""message"" : ""Malformed request""}");
            serializedResults[1].ShouldBe(@"""Single result string value""");
            serializedResults[2].ShouldBe(@"46");
            serializedResults[3].ShouldBe(@"{""wallet"":""WALLETNAME"",""nexus"":""NEXUSNAME"",""dapp"":""DAPP"",""token"":""TOKEN"",""version"":10}");
            // Lunar: serializedResults[3].ShouldBe(@"""Phantasma.Core.WalletLink+Authorization""");
            serializedResults[4].ShouldBe(@"{""alias"":""justauser"",""address"":""P2KJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJj"",""name"":""ACCOUNTNAME"",""avatar"":""todoputavatarhere"",""platform"":""PLATFORM"",""external"":null,""balances"":[{""symbol"":""SOUL"",""value"":""100000000000"",""decimals"":8},{""symbol"":""KCAL"",""value"":""10000000000000"",""decimals"":10}],""files"":[{""name"":""TestFile1"",""size"":1024,""date"":1638030670,""hash"":""0xAAAA""},{""name"":""TestFile2"",""size"":1000000,""date"":1638030770,""hash"":""0xBBBB""}]}");
            // Lunar: serializedResults[4].ShouldBe(@"""Phantasma.Core.WalletLink+Account""");
            serializedResults[5].ShouldBe(@"[""value1"",""value2"",""value3""]");
            // Lunar: serializedResults[5].ShouldBe(@"[{""value1""},{""value2""},{""value3""}]");
            serializedResults[6].ShouldBe(@"[""value1"",""value2"",""value3""]");
            // Lunar: serializedResults[6].ShouldBe(@"[{""value1""},{""value2""},{""value3""}]");
            serializedResults[7].ShouldBe(@"[1,""value2"",3]");
            // Lunar: serializedResults[7].ShouldBe(@"[{1},{""value2""},{3}]");
            serializedResults[8].ShouldBe(@"{""wallet"":""WALLETNAME"",""nexus"":""NEXUSNAME"",""dapp"":""DAPP"",""token"":""TOKEN"",""version"":10}");
            // Lunar: serializedResults[8].ShouldBe(@"{""wallet"" : ""WALLETNAME"",""nexus"" : ""NEXUSNAME"",""dapp"" : ""DAPP"",""token"" : ""TOKEN"",""version"" : 10}");
            serializedResults[9].ShouldBe(@"{""alias"":""justauser"",""address"":""P2KJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJj"",""name"":""ACCOUNTNAME"",""avatar"":""todoputavatarhere"",""platform"":""PLATFORM"",""external"":null,""balances"":[{""symbol"":""SOUL"",""value"":""100000000000"",""decimals"":8},{""symbol"":""KCAL"",""value"":""10000000000000"",""decimals"":10}],""files"":[{""name"":""TestFile1"",""size"":1024,""date"":1638030670,""hash"":""0xAAAA""},{""name"":""TestFile2"",""size"":1000000,""date"":1638030770,""hash"":""0xBBBB""}]}");
            // Lunar: serializedResults[9].ShouldBe(@"{""alias"" : ""justauser"",""address"" : ""P2KJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJj"",""name"" : ""ACCOUNTNAME"",""avatar"" : ""todoputavatarhere"",""platform"" : ""PLATFORM"",""balances"" : [{""symbol"" : ""SOUL"",""value"" : ""100000000000"",""decimals"" : 8},{""symbol"" : ""KCAL"",""value"" : ""10000000000000"",""decimals"" : 10}],""files"" : [{""name"" : ""TestFile1"",""size"" : 1024,""date"" : 1638030670,""hash"" : ""0xAAAA""},{""name"" : ""TestFile2"",""size"" : 1000000,""date"" : 1638030770,""hash"" : ""0xBBBB""}]}");
            serializedResults[10].ShouldBe(@"[{""wallet"":""WALLETNAME"",""nexus"":""NEXUSNAME"",""dapp"":""DAPP"",""token"":""TOKEN"",""version"":10},{""alias"":""justauser"",""address"":""P2KJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJj"",""name"":""ACCOUNTNAME"",""avatar"":""todoputavatarhere"",""platform"":""PLATFORM"",""external"":null,""balances"":[{""symbol"":""SOUL"",""value"":""100000000000"",""decimals"":8},{""symbol"":""KCAL"",""value"":""10000000000000"",""decimals"":10}],""files"":[{""name"":""TestFile1"",""size"":1024,""date"":1638030670,""hash"":""0xAAAA""},{""name"":""TestFile2"",""size"":1000000,""date"":1638030770,""hash"":""0xBBBB""}]},3]");
            // Lunar: serializedResults[10].ShouldBe(@"[{""Awallet"" : ""WALLETNAME"",""nexus"" : ""NEXUSNAME"",""dapp"" : ""DAPP"",""token"" : ""TOKEN"",""version"" : 10},{""alias"" : ""justauser"",""address"" : ""P2KJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJj"",""name"" : ""ACCOUNTNAME"",""avatar"" : ""todoputavatarhere"",""platform"" : ""PLATFORM"",""balances"" : [{""symbol"" : ""SOUL"",""value"" : ""100000000000"",""decimals"" : 8},{""symbol"" : ""KCAL"",""value"" : ""10000000000000"",""decimals"" : 10}],""files"" : [{""name"" : ""TestFile1"",""size"" : 1024,""date"" : 1638030670,""hash"" : ""0xAAAA""},{""name"" : ""TestFile2"",""size"" : 1000000,""date"" : 1638030770,""hash"" : ""0xBBBB""}]},{3}]");
        }
    }
}