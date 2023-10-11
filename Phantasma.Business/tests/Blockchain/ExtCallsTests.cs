using System;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Gas.Structs;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types.Structs;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class ExtCallsTests
{
    
    PhantasmaKeys user;
    PhantasmaKeys user2;
    PhantasmaKeys owner;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;
    StakeReward reward;
    
    private static string TEST_CONTRACT_PVM = "000D00040F52756E74696D652E56657273696F6E070004000D0103010E1A0001000A005F000D00043343757272656E74206E657875732070726F746F636F6C2076657273696F6E2073686F756C64206265203134206F72206D6F72650C0000040403040D000409416464726573732829070004040204050205020D050301000205010D05040461616161020503000D040408446174612E53657403010D00040269640300070403020D0004056F776E65720300070403030D00040C636F6E74726163744E616D65030007040B000D010408446174612E4765740D02040C6D696E74636F6E74726163740D0003010303000D000402696403000302070104030D0003010803000D0004056F776E6572030003020701040403040D00040941646472657373282907000404040103010D0004094164647265737328290700040102040503050D02041152756E74696D652E49735769746E65737307020402090275010D05040E7769746E657373206661696C65640C05000203020D05030101230205060206030D050214010B646F536F6D657468696E670300000000000003050D05026B000D02030400CA9A3B03020D0202220200F4CAF4FF95731A23E49CB9DDE141E8C6980EF5AF5F7DA847B7F802702239F36C03020D0004094164647265737328290700040203020D0104055374616B6503010D0104057374616B652D01012E010D010301000301086A00000B03050D05040C6D797365636F6E64636F6E7403050D05022202005E403A9E908A365C5CCEE4BFEFDF8256E49E6DD08D2396A79063C864FE6279F303050D0004094164647265737328290700040503050D02041652756E74696D652E4465706C6F79436F6E747261637407020203020302088102000D010408446174612E53657403030D0004026964030007010B00040103010D0004094164647265737328290700040104020D040301640204030D050301010205040D060704000000000206050D060404544553540206020D07024B04076765744E616D650400000000000A496E697469616C697A65001000000001065F6F776E6572080B646F536F6D657468696E6703A7000000000967657453796D626F6C0412010000000003070D0702FD2201000D010404746573740301080F00000B000D00040F52756E74696D652E56657273696F6E070004000D0103010E1A0001000A006F000D00043343757272656E74206E657875732070726F746F636F6C2076657273696F6E2073686F756C64206265203134206F72206D6F72650C0000040203020D00040941646472657373282907000402020203020301000D020408446174612E53657403010D0004056F776E6572030007020B000D02030400CA9A3B03020D0202220200F4CAF4FF95731A23E49CB9DDE141E8C6980EF5AF5F7DA847B7F802702239F36C03020D0004094164647265737328290700040203020D0104055374616B6503010D0104057374616B652D01012E010D010301000301081101000B000D010404544553540301082101000B03070205070307020407030702030703070D07040A5465737420546F6B656E030702020703070D07022202005E403A9E908A365C5CCEE4BFEFDF8256E49E6DD08D2396A79063C864FE6279F303070D0004094164647265737328290700040703070D0604114E657875732E437265617465546F6B656E0706000B";
    private static string TEST_CONTRACT_ABI = "030A496E697469616C697A650000000000010D636F6E74726163744F776E6572080E6D696E744D79436F6E747261637403CC000000010466726F6D080B6D696E744D79546F6B656E009A020000020466726F6D0809746F6B656E4E616D650400";
    
    public ExtCallsTests()
    {
        Initialize();
    }

    private void Initialize()
    {
        user = PhantasmaKeys.Generate();
        user2 = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(20000, DomainSettings.FuelTokenDecimals);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
        
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(new []{owner}, 16);
        nexus = simulator.Nexus;
        nexus.SetOracleReader(new OracleSimulator(nexus));
        SetInitialBalance(user.Address);
    }

    protected void SetInitialBalance(Address address)
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, initialAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }

    [Fact]
    public void TestDeployContractInsideContract()
    {
        var contractName = "mintcontract";
        var secondContractName = "mysecondcont";
        DeployContract(user, user.Address, user.Address, contractName, TEST_CONTRACT_PVM, TEST_CONTRACT_ABI, false);

        var contract = simulator.Nexus.GetContractByName(simulator.Nexus.RootStorage, contractName);
        // Send Tokens to Contract
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, contract.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        simulator.BeginBlock(); 
        var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal,
            () => ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, 1000000)
                .CallContract(contractName, "mintMyContract", user.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        var mySecondContract = simulator.Nexus.GetContractByName(simulator.Nexus.RootStorage, secondContractName);
        Assert.NotNull(mySecondContract);
        
        
        simulator.BeginBlock(); 
        tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal,
            () => ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, 1000000)
                .CallContract(secondContractName, "doSomething")
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(!simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);


    }


    [Fact(Skip = "TODO")]
    public void TestDeployToken()
    {
        var contractName = "mintcontract";
        var secondContractName = "TETT";
        DeployContract(user, user.Address, user.Address, contractName, TEST_CONTRACT_PVM, TEST_CONTRACT_ABI, false);

        var contract = simulator.Nexus.GetContractByName(simulator.Nexus.RootStorage, contractName);
        // Send Tokens to Contract
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, contract.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, contract.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(20000, DomainSettings.StakingTokenDecimals));
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        simulator.BeginBlock(); 
        var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal,
            () => ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, 1000000)
                .CallContract(contractName, "mintMyToken", user.Address, secondContractName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        var mySecondContract = simulator.Nexus.GetContractByName(simulator.Nexus.RootStorage, secondContractName);
        Assert.NotNull(mySecondContract);
        
        simulator.BeginBlock(); 
        tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal,
            () => ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, 1000000)
                .CallContract(secondContractName, "doSomething")
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
    }
    // List_Clear
    // Data_Delete
    // Map_Has
    // Map_Get
    // Map_Remove
    // Map_Keys
    // Map_Count
    // List_Add
    // List_Get
    // List_Replace
    // List_RemoveAt
    // List_Count
    
    
    private BigInteger CreateToken(PhantasmaKeys _user, Address gasAddress, Address userAddress, string contractName,
        byte[] contractPVM, byte[] contractABI, bool shouldFail = false)
    {
        simulator.BeginBlock();
        var tx = simulator.GenerateCustomTransaction(_user, ProofOfWork.Minimal,
            () => ScriptUtils.BeginScript()
                .AllowGas(gasAddress, Address.Null, simulator.MinimumFee, 1000000)
                .CallInterop("Nexus.CreateToken", userAddress, contractPVM, contractABI)
                .SpendGas(gasAddress)
                .EndScript());
        simulator.EndBlock();
        if (shouldFail)
            Assert.False(simulator.LastBlockWasSuccessful(), $"Create {contractName} Token Passed... ISSUE");
        else
            Assert.True(simulator.LastBlockWasSuccessful(), $"Create {contractName} Token Passed... ISSUE");
        
        if ( user.Address == gasAddress)
            return simulator.Nexus.RootChain.GetTransactionFee(tx);
        return 0;
    }

    private BigInteger CreateToken(PhantasmaKeys _user, Address gasAddress, Address userAddress, string contractName,
        string contractPVM, string contractABI, bool shouldFail = false)
    {
        return CreateToken(_user, gasAddress, userAddress, contractName, Base16.Decode(contractPVM),
            Base16.Decode(contractABI), shouldFail);
    }
    
    private BigInteger DeployContract(PhantasmaKeys _user, Address gasAddress, Address userAddress, string contractName, byte[] contractPVM, byte[] contractABI, bool shouldFail = false)
    {
        simulator.BeginBlock(); 
        var tx = simulator.GenerateCustomTransaction(_user, ProofOfWork.Minimal,
            () => ScriptUtils.BeginScript()
                .AllowGas(gasAddress, Address.Null, simulator.MinimumFee, 1000000)
                .CallInterop("Runtime.DeployContract", userAddress, contractName, contractPVM, contractABI)
                .SpendGas(gasAddress)
                .EndScript());
        simulator.EndBlock();
        if (shouldFail)
            Assert.False(simulator.LastBlockWasSuccessful(), $"Deploying {contractName} Contract Passed... ISSUE");
        else
            Assert.True(simulator.LastBlockWasSuccessful(), $"Deploying {contractName} Contract Passed... ISSUE");
        //var txCost2 = simulator.Nexus.RootChain.GetTransactionFee(tx);
        if ( user.Address == gasAddress)
            return simulator.Nexus.RootChain.GetTransactionFee(tx);
        return 0;
    }
    
    private BigInteger DeployContract(PhantasmaKeys _user, Address gasAddress, Address userAddress, string contractName,
        string contractPVM, string contractABI, bool shouldFail = false)
    {
        return DeployContract(_user, gasAddress, userAddress, contractName, Base16.Decode(contractPVM),
            Base16.Decode(contractABI), shouldFail);
    }
}
