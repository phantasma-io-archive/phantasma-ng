using Phantasma.Core;
using Shouldly;
using System.Collections.Generic;
using Xunit;
using static Phantasma.Core.WalletLink;

namespace GhostDevs.MP.Tests
{

    public class IapiResultSerializationTests
    {
        [Fact]
        public void Iapiresult_serialization_works_correctly()
        {
            // Arrange
            var authorization = new Authorization() { wallet = "WALLETNAME", nexus = "NEXUSNAME", dapp = "DAPP", token = "TOKEN", version = 10 };
            var account = new Account() { address = "P2KJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJj", alias = "justauser", avatar = "todoputavatarhere", external = null, name = "ACCOUNTNAME", platform = "PLATFORM", balances = new Balance[] { new Balance() { symbol = "SOUL", decimals = 8, value = "100000000000" }, new Balance() { symbol = "KCAL", decimals = 10, value = "10000000000000" } }, files = new File[] { new File() { name = "TestFile1", date = 1638030670, hash = "0xAAAA", size = 1024 }, new File() { name = "TestFile2", date = 1638030770, hash = "0xBBBB", size = 1000000 } } };

            var apiResults = new LunarLabs.Parser.DataNode[] {
                APIUtils.FromAPIResult(new Error() { message = "Malformed request" }),
                APIUtils.FromAPIResult(new SingleResult() { value = "Single result string value" }),
                APIUtils.FromAPIResult(new SingleResult() { value = 46 }),
                APIUtils.FromAPIResult(new SingleResult() { value = authorization }),
                APIUtils.FromAPIResult(new SingleResult() { value = account }),
                APIUtils.FromAPIResult(new ArrayResult() { values = new string[]{ "value1", "value2", "value3" } }),
                APIUtils.FromAPIResult(new ArrayResult() { values = new object[]{ "value1", "value2", "value3" } }),
                APIUtils.FromAPIResult(new ArrayResult() { values = new object[]{ 1, "value2", 3 } }),
                APIUtils.FromAPIResult(authorization),
                APIUtils.FromAPIResult(account),
                APIUtils.FromAPIResult(new ArrayResult() { values = new object[]{ authorization, account, 3 } }),
            };

            // Act
            var serializedResultsList = new List<string>();
            for(var i = 0; i < apiResults.Length; i++)
            {
                serializedResultsList.Add(LunarLabs.Parser.DataFormats.SaveToString(LunarLabs.Parser.DataFormat.JSON, apiResults[i]));
            }
            var serializedResults = serializedResultsList.ToArray();

            // Assert
            serializedResults[0].ShouldBeEquivalentTo(@"{""message"" : ""Malformed request""}");
            serializedResults[1].ShouldBeEquivalentTo(@"""Single result string value""");
            serializedResults[2].ShouldBeEquivalentTo(@"46");
            serializedResults[3].ShouldBeEquivalentTo(@"""Phantasma.Core.WalletLink+Authorization""");
            serializedResults[4].ShouldBeEquivalentTo(@"""Phantasma.Core.WalletLink+Account""");
            serializedResults[5].ShouldBeEquivalentTo(@"[{""value1""},{""value2""},{""value3""}]");
            serializedResults[6].ShouldBeEquivalentTo(@"[{""value1""},{""value2""},{""value3""}]");
            serializedResults[7].ShouldBeEquivalentTo(@"[{1},{""value2""},{3}]");
            serializedResults[8].ShouldBeEquivalentTo(@"{""wallet"" : ""WALLETNAME"",""nexus"" : ""NEXUSNAME"",""dapp"" : ""DAPP"",""token"" : ""TOKEN"",""version"" : 10}");
            serializedResults[9].ShouldBeEquivalentTo(@"{""alias"" : ""justauser"",""address"" : ""P2KJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJj"",""name"" : ""ACCOUNTNAME"",""avatar"" : ""todoputavatarhere"",""platform"" : ""PLATFORM"",""balances"" : [{""symbol"" : ""SOUL"",""value"" : ""100000000000"",""decimals"" : 8},{""symbol"" : ""KCAL"",""value"" : ""10000000000000"",""decimals"" : 10}],""files"" : [{""name"" : ""TestFile1"",""size"" : 1024,""date"" : 1638030670,""hash"" : ""0xAAAA""},{""name"" : ""TestFile2"",""size"" : 1000000,""date"" : 1638030770,""hash"" : ""0xBBBB""}]}");
            serializedResults[10].ShouldBeEquivalentTo(@"[{""wallet"" : ""WALLETNAME"",""nexus"" : ""NEXUSNAME"",""dapp"" : ""DAPP"",""token"" : ""TOKEN"",""version"" : 10},{""alias"" : ""justauser"",""address"" : ""P2KJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJjJj"",""name"" : ""ACCOUNTNAME"",""avatar"" : ""todoputavatarhere"",""platform"" : ""PLATFORM"",""balances"" : [{""symbol"" : ""SOUL"",""value"" : ""100000000000"",""decimals"" : 8},{""symbol"" : ""KCAL"",""value"" : ""10000000000000"",""decimals"" : 10}],""files"" : [{""name"" : ""TestFile1"",""size"" : 1024,""date"" : 1638030670,""hash"" : ""0xAAAA""},{""name"" : ""TestFile2"",""size"" : 1000000,""date"" : 1638030770,""hash"" : ""0xBBBB""}]},{3}]");
        }
    }
}