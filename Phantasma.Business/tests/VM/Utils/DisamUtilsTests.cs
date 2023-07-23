using System.Collections.Generic;
using System.Linq;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.VM;
using Xunit;

namespace Phantasma.Business.Tests.VM.Utils;

[Collection(nameof(SystemTestCollectionDefinition))]
public class DisamUtilsTests
{
    [Fact]
    public void TestToString()
    {
        // Arrange
        var contractName = "TestContract";
        var methodName = "TestMethod";
        var arguments = new VMObject[] { new VMObject(), new VMObject() };
        var disasmMethodCall = new DisasmMethodCall
        {
            ContractName = contractName,
            MethodName = methodName,
            Arguments = arguments
        };

        // Act
        var result = disasmMethodCall.ToString();

        // Assert
        Assert.Equal($"{contractName}.{methodName}({arguments[0].ToString()},{arguments[1].ToString()})", result);
    }
    
    [Fact(Skip = "Not implemented")]
    public void TestPopArgs()
    {
        // Arrange
        var contract = "TestContract";
        var method = "TestMethod";
        var stack = new Stack<VMObject>();
        stack.Push(new VMObject());
        stack.Push(new VMObject());
        var methodArgumentCountTable = new Dictionary<string, int>();
        methodArgumentCountTable.Add($"{contract}.{method}", 2);

        // Act
        //var result = DisasmUtils.PopArgs(contract, method, stack, methodArgumentCountTable);

        // Assert
        //Assert.Equal(2, result.Length);
    }

    [Fact(Skip = "Don't do it")]
    public void TestGetDefaultDisasmTable()
    {
        // Act
        var result = DisasmUtils.GetDefaultDisasmTable();

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact ()]
    public void TestAddContractToTable()
    {
        // Arrange
        var table = new Dictionary<string, int>();
        var contract = new SwapContract();

        // Act
        DisasmUtils.AddContractToTable(table, contract);

        // Assert
        Assert.True(table.ContainsKey($"{contract.Name}.{contract.ABI.Methods.First().name}"));
    }
}
