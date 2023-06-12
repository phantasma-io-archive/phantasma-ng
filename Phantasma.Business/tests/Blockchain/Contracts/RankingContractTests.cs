using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.LeaderboardDetails;
using Phantasma.Core.Numerics;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

using Xunit;
using Phantasma.Business.Blockchain.Contracts.Native;

[Collection(nameof(SystemTestCollectionDefinition))]
public class RankingContractTests
{
    PhantasmaKeys user;
    PhantasmaKeys user2;
    PhantasmaKeys user3;
    PhantasmaKeys owner;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;

    public RankingContractTests()
    {
        Initialize();
    }

    public void Initialize()
    {
        user = PhantasmaKeys.Generate();
        user2 = PhantasmaKeys.Generate();
        user3 = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
    
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(owner);
        nexus = simulator.Nexus;
        nexus.SetOracleReader(new OracleSimulator(nexus));
        SetInitialBalance(user.Address);
        SetInitialBalance(user2.Address);
        SetInitialBalance(user3.Address);
    }

    protected void SetInitialBalance(Address address)
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, initialAmount);
        simulator.EndBlock();
        
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 100000000000);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }

    private void SetupLeaderboard()
    {
        var numberOfEntries = 10;
        var leaderboardName = "testboard";
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Ranking, nameof(RankingContract.CreateLeaderboard), user.Address, leaderboardName, numberOfEntries)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }

    private void InsertEntry(string leaderboardName, Address userScore, BigInteger score)
    {
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Ranking, nameof(RankingContract.InsertScore), user.Address,
                    userScore, leaderboardName, score)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
    [Fact]
    public void TestExists()
    {
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Ranking, nameof(RankingContract.Exists), "test")
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
    [Fact]
    public void TestCreateLeaderboard()
    {
        var numberOfEntries = 10;
        var leaderboardName = "testboard";

        SetupLeaderboard();

        var leaderboard = simulator.InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetLeaderboard), leaderboardName)
            .AsStruct<Leaderboard>();
        
        Assert.Equal(leaderboardName, leaderboard.name);
        Assert.Equal(numberOfEntries, leaderboard.size); 
        Assert.Equal(user.Address, leaderboard.owner);
        Assert.Equal(0, leaderboard.round);
    }
    
    [Fact]
    public void TestInsert()
    {
        var numberOfEntries = 10;
        var leaderboardName = "testboard";
        var score = 100;
        
        // Setup leaderboard
        SetupLeaderboard();
       
        var leaderboardBefore = simulator.InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetLeaderboard), leaderboardName)
            .AsStruct<Leaderboard>();
        
        var leaderboardRowBefore = simulator.InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetRows), leaderboardName)
            .ToArray<LeaderboardRow>();
        
        // Insert entry
        InsertEntry(leaderboardName, user2.Address, score);

        var leaderboardAfter = simulator.InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetLeaderboard), leaderboardName)
            .AsStruct<Leaderboard>();
        
        var leaderboardRowAfter = simulator.InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetRows), leaderboardName)
            .ToArray<LeaderboardRow>();
        
        Assert.Equal(leaderboardName, leaderboardAfter.name);
        Assert.Equal(numberOfEntries, leaderboardAfter.size); 
        Assert.Equal(user.Address, leaderboardAfter.owner);
        Assert.NotEqual(leaderboardRowBefore, leaderboardRowAfter);
        Assert.Equal(leaderboardRowBefore.Length + 1, leaderboardRowAfter.Length);
        Assert.Equal(user2.Address, leaderboardRowAfter.First().address);
        Assert.Equal(score, leaderboardRowAfter.First().score);
        
        // Fail Insert entry
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user2, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Ranking, nameof(RankingContract.InsertScore),  user.Address, user2.Address, leaderboardName, score)
                .SpendGas(user2.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
        
        // Test change score
        var newScore = 200;
        InsertEntry(leaderboardName, user2.Address, newScore);
        
        var leaderboardRowAfterChange = simulator.InvokeContract(NativeContractKind.Ranking,
            nameof(RankingContract.GetRows), leaderboardName).ToArray<LeaderboardRow>();
        
        Assert.Equal(newScore, leaderboardRowAfterChange.First().score);
    }

    [Fact]
    public void TestInsertWithScore()
    {
        var numberOfEntries = 10;
        var leaderboardName = "testboard";
        var score = 100;
        var scoreBase = 10;

        // Setup leaderboard
        SetupLeaderboard();
        
        // Insert entry
        InsertEntry(leaderboardName, user2.Address, score);
        InsertEntry(leaderboardName, user3.Address, score*2);
        InsertEntry(leaderboardName, owner.Address, score*10);

        var leaderboardAfter = simulator.InvokeContract(NativeContractKind.Ranking,
                nameof(RankingContract.GetLeaderboard), leaderboardName)
            .AsStruct<Leaderboard>();

        var leaderboardRowAfter = simulator
            .InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetRows), leaderboardName)
            .ToArray<LeaderboardRow>();
        
        var leaderboardSize = simulator.InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetSize), leaderboardName)
            .AsNumber();
        
        Assert.Equal(owner.Address, leaderboardRowAfter.First().address);
        Assert.Equal(score*10, leaderboardRowAfter.First().score);
        Assert.Equal(user3.Address, leaderboardRowAfter[1].address);
        Assert.Equal(score*2, leaderboardRowAfter[1].score);
        Assert.Equal(user2.Address, leaderboardRowAfter[2].address);
        Assert.Equal(score, leaderboardRowAfter[2].score);
        Assert.Equal(3, leaderboardSize);
        
        // Add more users
        List<PhantasmaKeys> users = new List<PhantasmaKeys>();
        for (int i = 0; i < 20; i++)
        {
            users.Add(PhantasmaKeys.Generate());
            InsertEntry(leaderboardName, users[i].Address, scoreBase * i);
        }
        
        leaderboardRowAfter = simulator
            .InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetRows), leaderboardName)
            .ToArray<LeaderboardRow>();
        
        leaderboardSize = simulator.InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetSize), leaderboardName)
            .AsNumber();
        
        Assert.Equal(10, leaderboardSize);
        Assert.Equal(users[0].Address, leaderboardRowAfter.Last().address);
        Assert.Equal(scoreBase * 0 , leaderboardRowAfter.Last().score);
        

        // Get's
        var ownerScore = simulator
            .InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetScoreByAddress), leaderboardName, owner.Address)
            .AsNumber();
        
        var user3Score = simulator
            .InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetScoreByIndex), leaderboardName, 1)
            .AsNumber();
        
        var user3Address = simulator
            .InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetAddressByIndex), leaderboardName, 1)
            .AsAddress();
        
        Assert.Equal(score*10, ownerScore);
        Assert.Equal(score*2, user3Score);
        Assert.Equal(user3.Address, user3Address);
        
    }

    [Fact]
    public void TestResetLeaderboard()
    {
        var numberOfEntries = 10;
        var leaderboardName = "testboard";
        var score = 100;
        var scoreBase = 10;

        // Setup leaderboard
        SetupLeaderboard();

        // Insert entry
        InsertEntry(leaderboardName, user2.Address, score);
        InsertEntry(leaderboardName, user3.Address, score * 2);
        InsertEntry(leaderboardName, owner.Address, score * 10);

        var leaderboardAfter = simulator.InvokeContract(NativeContractKind.Ranking,
                nameof(RankingContract.GetLeaderboard), leaderboardName)
            .AsStruct<Leaderboard>();

        var leaderboardRowAfter = simulator
            .InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetRows), leaderboardName)
            .ToArray<LeaderboardRow>();

        var leaderboardSize = simulator
            .InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetSize), leaderboardName)
            .AsNumber();

        Assert.Equal(owner.Address, leaderboardRowAfter.First().address);
        Assert.Equal(score * 10, leaderboardRowAfter.First().score);
        Assert.Equal(user3.Address, leaderboardRowAfter[1].address);
        Assert.Equal(score * 2, leaderboardRowAfter[1].score);
        Assert.Equal(user2.Address, leaderboardRowAfter[2].address);
        Assert.Equal(score, leaderboardRowAfter[2].score);
        Assert.Equal(3, leaderboardSize);
        
        // Reset leaderboard
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Ranking, nameof(RankingContract.ResetLeaderboard), user.Address, leaderboardName)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // Add more users
        List<PhantasmaKeys> users = new List<PhantasmaKeys>();
        for (int i = 0; i < 20; i++)
        {
            users.Add(PhantasmaKeys.Generate());
            InsertEntry(leaderboardName, users[i].Address, scoreBase * i);
        }

        leaderboardRowAfter = simulator
            .InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetRows), leaderboardName)
            .ToArray<LeaderboardRow>();

        leaderboardSize = simulator
            .InvokeContract(NativeContractKind.Ranking, nameof(RankingContract.GetSize), leaderboardName)
            .AsNumber();

        Assert.Equal(10, leaderboardSize);
        Assert.NotEqual(owner.Address, leaderboardRowAfter.First().address);
        Assert.NotEqual(score * 10, leaderboardRowAfter.First().score);
    }
}
