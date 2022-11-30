/*
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.RocksDB;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class InteropTests : IDisposable
{
    private PhantasmaKeys Validator { get; set; }
    private PhantasmaKeys TokenOwner { get; set; }
    private PhantasmaKeys User1 { get; set; }
    private PhantasmaKeys User2 { get; set; }

    private string PartitionPath { get; set; }
    private IToken FungibleToken { get; set; }
    private IToken NonFungibleToken { get; set; }
    private IToken NonTransferableToken { get; set; }
    private IChain Chain { get; set; }
    private Dictionary<string, BigInteger> Mints { get; set; }

    //[TestMethod]
    //public void invoke_Oracle_Quote_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Oracle.Quote", );
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Oracle_Price_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Oracle.Price");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Oracle_Read_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Oracle.Read");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    [Fact]
    public void invoke_Account_Transactions_success_non_empty()
    {
        var runtime = CreateRuntime_MockedChain();
        var result = runtime.CallInterop("Account.Transactions", User1.Address);
        result.Type.ShouldBe(VMType.Struct);
        var dict = (Dictionary<VMObject, VMObject>)result.Data;
        dict.Count.ShouldBe(5);
        for (var i = 0; i < 5; i++)
        {
            var obj = new VMObject().SetValue(i);
            var val = (Hash)dict[obj].Data;
            val.ShouldBe(Hash.FromString(i.ToString()));
        }
    }

    [Fact]
    public void invoke_Account_Activity_success()
    {
        var runtime = CreateRuntime_MockedChain();
        var result = runtime.CallInterop("Account.LastActivity", User1.Address);
        result.Type.ShouldBe(VMType.Timestamp);
        result.AsTimestamp().ShouldBe(new Timestamp(1601092859));
    }

    [Fact]
    public void invoke_Account_Name_success()
    {
        var runtime = CreateRuntime_MockedChain();
        var result = runtime.CallInterop("Account.Name", User1.Address);
        result.AsString().ShouldBe(User1.Address.Text);
        result.AsString().ShouldBe(User1.Address.ToString());

        result = runtime.CallInterop("Account.Name", User2.Address);
        result.AsString().ShouldBe(User2.Address.Text);
        result.AsString().ShouldBe(User2.Address.ToString());

        result = runtime.CallInterop("Account.Name", TokenOwner.Address);
        result.AsString().ShouldBe(TokenOwner.Address.Text);
        result.AsString().ShouldBe(TokenOwner.Address.ToString());
    }

    //[TestMethod]
    //public void invoke_List_Clear_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("List.Clear");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_List_Count_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("List.Count");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_List_RemoveAt_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("List.RemoveAt");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_List_Replace_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("List.Replace");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_List_Add_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("List.Add");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_List_Get_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("List.Get");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Map_Keys_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Map.Keys");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Map_Clear_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Map.Clear");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Map_Count_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Map.Count");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Map_Remove_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Map.Remove");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Map_Set_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Map.Set");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Map_Get_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Map.Get");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Map_Has_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Map.Has");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Data_Delete_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Data.Delete");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Data_Set_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Data.Set");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Data_Get_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Data.Get");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Task_Current_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Task.Current");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Task_Get_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Task.Get");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Task_Stop_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Task.Stop");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Task_Start_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Task.Start");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    [Fact]
    public void invoke_Organization_AddMember_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Organization.AddMember", Validator.Address, "someorgid", User1.Address);

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.OrganizationAdd);
            var content = evt.GetContent<OrganizationEventData>();
            content.Organization.ShouldBe("someorgid");
            content.MemberAddress.ShouldBe(User1.Address);
        }
    }

    /*
    [Fact]
    public void invoke_Nexus_CreateOrganization_success()
    {
        var contract = new CustomContract(NonFungibleToken.Symbol, NonFungibleToken.Script, NonFungibleToken.ABI);
        var context = new ChainExecutionContext(contract);
        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            new byte[1] { 0 },
            User1.Address,
            User1.Address,
            10000,
            999,
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        tx.Sign(Validator);

        var runtime = CreateRuntime( false, NonFungibleToken, context, tx, true, true);
        // TODO remove genesis address requirement
        var result = runtime.CallInterop("Nexus.CreateOrganization", Validator.Address, "someorgid", "someOrgName", new byte[] {1, 2, 3});

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.OrganizationCreate);
            var content = evt.GetContent<string>();
            content.ShouldBe("someorgid");
        }

        count.ShouldBe(1);
    }*/

    // currently unused
    //[TestMethod]
    //public void invoke_Nexus_CreateChain_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Nexus.CreateChain");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    /*
    [Fact]
    public void invoke_Nexus_CreateTokenSeries_success()
    {
        var contract = new CustomContract(NonFungibleToken.Symbol, NonFungibleToken.Script, NonFungibleToken.ABI);
        var context = new ChainExecutionContext(contract);
        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            new byte[1] { 0 },
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        tx.Sign(User1);

        var runtime = CreateRuntime( false, NonFungibleToken, context, tx, true, true);

        var result = runtime.CallInterop("Nexus.CreateTokenSeries",
                User1.Address,
                NonFungibleToken.Symbol,
                new BigInteger(1),
                new BigInteger(100),
                TokenSeriesMode.Unique,
                new byte[2] { (byte)Opcode.NOP, (byte)Opcode.RET },
                new byte[2] { 0, 0}
                );
    }

    // TODO::
    //[TestMethod]
    //public void invoke_Nexus_CreateToken_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Nexus.CreateToken");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Nexus_EndInit_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Nexus.EndInit");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Nexus_BeginInit_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Nexus.BeginInit");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Nexus_GetGovernanceValue_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Nexus.GetGovernanceValue");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_TokenGetFlags_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.TokenGetFlags");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_TokenGetDecimals_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.TokenGetDecimals");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_TokenExists_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.TokenExists");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_WriteToken_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.WriteToken");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_ReadToken_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.ReadToken");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_ReadTokenRAM_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.ReadTokenRAM");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_ReadTokenROM_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.ReadTokenROM");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_InfuseToken_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.InfuseToken");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_BurnToken_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.BurnToken");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_MintToken_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.MintToken");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_TransferToken_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.TransferToken");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_SwapTokens_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.SwapTokens");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_BurnTokens_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.BurnTokens");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_TransferBalance_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.TransferBalance");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_TransferTokens_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.TransferTokens");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_GetBalance_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.GetBalance");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_KillContract_success()
    //{
    //    throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_UpgradeContract_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.UpgradeContract");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    //[TestMethod]
    //public void invoke_Runtime_DeployContract_success()
    //{
    //    var runtime = CreateRuntime_Default();
    //    var result = runtime.CallInterop("Runtime.DeployContract");
    //    //throw new NotImplementedException("Unit test empty!");
    //}

    [Fact]
    public void invoke_Runtime_Notify_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.Notify", EventKind.Custom, User1.Address, "eventData");
        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.Custom);
            var content = evt.GetContent<VMObject>();
            evt.Address.ShouldBe(User1.Address);
            content.AsString().ShouldBe("eventData");
        }
        count.ShouldBe(1);
    }

    
    /*[Fact]
    public void invoke_Runtime_Log_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.Log", "some log");
        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.Log);
        }
        count.ShouldBe(1);
    }

    [Fact]
    public void invoke_Runtime_IsMinter_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.IsMinter", TokenOwner.Address, FungibleToken.Symbol);
        result.AsBool().ShouldBe(true);
    }

    [Fact]
    public void invoke_Runtime_IsMinter_fail()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.IsMinter", User1.Address, FungibleToken.Symbol);
        result.AsBool().ShouldBe(false);

        result = runtime.CallInterop("Runtime.IsMinter", User2.Address, FungibleToken.Symbol);
        result.AsBool().ShouldBe(false);

        result = runtime.CallInterop("Runtime.IsMinter", Validator.Address, FungibleToken.Symbol);
        result.AsBool().ShouldBe(false);

        var contract = new CustomContract(FungibleToken.Symbol, FungibleToken.Script, FungibleToken.ABI);

        result = runtime.CallInterop("Runtime.IsMinter", contract.Address, FungibleToken.Symbol);
        result.AsBool().ShouldBe(false);
    }

    [Fact]
    public void invoke_Runtime_IsTrigger_success_yes()
    {
        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            new byte[1] { 0 },
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        tx.Sign(User1);

        var runtime = CreateRuntime_Default(true);
        var result = runtime.CallInterop("Runtime.IsTrigger");
        result.AsBool().ShouldBe(true);
    }

    [Fact]
    public void invoke_Runtime_IsTrigger_success_no()
    {
        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            new byte[1] { 0 },
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        tx.Sign(User1);

        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.IsTrigger");
        result.AsBool().ShouldBe(false);
    }

    public void invoke_Runtime_IsWitness_other_cases()
    {
        throw new NotImplementedException("dummy test, need test IsWitness more");
    }

    [Fact]
    public void invoke_Runtime_IsWitness_success()
    {
        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            new byte[1] { 0 },
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        tx.Sign(User1);

        var runtime = CreateRuntime_Default(false, tx);
        var result = runtime.CallInterop("Runtime.IsWitness", User1.Address);
        result.AsBool().ShouldBe(true);

        result = runtime.CallInterop("Runtime.IsWitness", User2.Address);
        result.AsBool().ShouldBe(false);

        result = runtime.CallInterop("Runtime.IsWitness", TokenOwner.Address);
        result.AsBool().ShouldBe(false);

        result = runtime.CallInterop("Runtime.IsWitness", Validator.Address);
        result.AsBool().ShouldBe(false);

        var contract = new CustomContract(FungibleToken.Symbol, FungibleToken.Script, FungibleToken.ABI);
        result = runtime.CallInterop("Runtime.IsWitness", contract.Address);
        result.AsBool().ShouldBe(true); // current context, so true

        contract = new CustomContract(NonFungibleToken.Symbol, NonFungibleToken.Script, NonFungibleToken.ABI);
        result = runtime.CallInterop("Runtime.IsWitness", contract.Address);
        result.AsBool().ShouldBe(false);
    }

    [Fact]
    public void invoke_Runtime_Random_success_tx_hash()
    {
        var runtime = CreateRuntime_Default();
        var seed = BigInteger.Parse("18458728232664014981834504770148603574620012689608654470072532441766883911609642");
        var result = runtime.CallInterop("Runtime.Random");

        result.AsNumber().ShouldBeGreaterThan(BigInteger.Zero);

        var seed2 = BigInteger.Parse("18458728232664014981834504770148603574620012689608654470072532441766883911609642");
        var result2 = runtime.CallInterop("Runtime.Random");

        result.AsNumber().ShouldNotBe(result2.AsNumber());

        var seed3 = BigInteger.Parse("32948902349023489290348901238490123904890213748712389478912738947891234789213789");
        var result3 = runtime.CallInterop("Runtime.Random");

        result.AsNumber().ShouldNotBe(result3.AsNumber());
        result2.AsNumber().ShouldNotBe(result3.AsNumber());
    }

    [Fact]
    public void invoke_Runtime_GenerateUID_success()
    {
        var runtime = CreateRuntime_MockedChain();
        var result = runtime.CallInterop("Runtime.GenerateUID");
        result.AsNumber().ShouldBe(1200);
    }

    [Fact]
    public void invoke_Runtime_PreviousContext_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.PreviousContext");
        result.AsString().ShouldBe(VirtualMachine.EntryContextName);
    }

    [Fact]
    public void invoke_Runtime_Context_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.Context");
        result.AsString().ShouldBe(FungibleToken.Symbol);
    }

    [Fact]
    public void invoke_Runtime_Validator_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.Validator");
        result.AsAddress().ShouldBe(this.Validator.Address);
    }

    [Fact]
    public void invoke_Runtime_GasTarget_fail_is_null()
    {
        var runtime = CreateRuntime_Default();
        Should.Throw<VMException>(() => runtime.CallInterop("Runtime.GasTarget"), "Gas target is not available yet");
    }

    [Fact]
    public void invoke_Runtime_GasTarget_success()
    {
        var runtime = CreateRuntime_Default();
        var gasInfo = new GasEventData(User1.Address, 10000, 10000);
        runtime.Notify(EventKind.GasEscrow, User1.Address, Serialization.Serialize(gasInfo), NativeContractKind.Gas.GetContractName());
        var result = runtime.CallInterop("Runtime.GasTarget");
        var address = (Address)result.Data;
        address.ShouldBe(User1.Address);
    }


    [Fact]
    public void invoke_Runtime_Version_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.Version");
        result.AsNumber().ShouldBe(new BigInteger(0));
    }

    [Fact]
    public void invoke_Runtime_Time_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.Time");
        result.ShouldNotBeNull();
        runtime.Time.ShouldBe(result.AsTimestamp());
        result.Type.ShouldBe(VMType.Timestamp);
    }

    [Fact]
    public void invoke_Runtime_TransactionHash_success()
    {
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.TransactionHash");
        result.ShouldNotBeNull();
        result.Type.ShouldBe(VMType.Object);
    }

    [Fact]
    public void invoke_Runtime_TransactionHash_fail()
    {
        var runtime = CreateRuntime(false, FungibleToken);
        Should.Throw<VMException>(() => runtime.CallInterop("Runtime.TransactionHash"), "Value cannot be null. (Parameter 'tx')");
    }

    [Fact]
    public void invoke_Runtime_MintTokens_success()
    {
        var toMint = new BigInteger(999);
        var runtime = CreateRuntime_Default();
        var result = runtime.CallInterop("Runtime.MintTokens", TokenOwner.Address, User1.Address, FungibleToken.Symbol, toMint);
        result.ShouldBeNull();
        this.Mints.Count.ShouldBe(1);

        var amount = this.Mints[FungibleToken.Symbol];
        amount.ShouldBe(toMint);
    }

    [Fact]
    public void invoke_Runtime_MintTokens_failed_not_allowed()
    {
        var toMint = new BigInteger(999);
        var runtime = CreateRuntime_Default();
        Should.Throw<VMException>(
            () => runtime.CallInterop("Runtime.MintTokens", User1.Address, User1.Address, FungibleToken.Symbol, toMint),
            $"{User1.Address} is not a valid minting address for {FungibleToken.Symbol} @ Runtime_MintTokens");
    }

    private IRuntime CreateRuntime_MockedChain()
    {
        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            new byte[1] { 0 },
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        tx.Sign(User1);
        tx.Sign(User2);
        tx.Sign(TokenOwner);

        var contract = new CustomContract(FungibleToken.Symbol, FungibleToken.Script, FungibleToken.ABI);
        var context = new ChainExecutionContext(contract);
        return CreateRuntime(false, FungibleToken, context, tx, true, true);
    }

    private IRuntime CreateRuntime_Default(bool delayPayment = false, Transaction tx = null, bool tokenExists = true)
    {
        if (tx == null)
        {
            tx = new Transaction(
                "mainnet",
                DomainSettings.RootChainName,
                new byte[1] { 0 },
                Timestamp.Now + TimeSpan.FromDays(300),
                "UnitTest");

            tx.Sign(User1);
            tx.Sign(User2);
            tx.Sign(TokenOwner);
        }

        var contract = new CustomContract(FungibleToken.Symbol, FungibleToken.Script, FungibleToken.ABI);
        var context = new ChainExecutionContext(contract);
        return CreateRuntime(delayPayment, FungibleToken, context, tx, false, tokenExists);
    }

    private IRuntime CreateRuntime(
            bool delayPayment,
            IToken token,
            ExecutionContext context = null,
            Transaction tx = null,
            bool mockChain = false,
            bool tokenExists = true)
    {
        var nexusMoq = new Mock<INexus>();
        var storage = new StorageChangeSetContext(new KeyStoreStorage(new DBPartition(this.PartitionPath)));

        nexusMoq.Setup( n => n.GetOrganizationByName(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns(new Organization("someorgid", storage));

        nexusMoq.Setup( n => n.TransferTokens(
                    It.IsAny<IRuntime>(),
                    It.IsAny<IToken>(),
                    It.IsAny<Address>(),
                    It.IsAny<Address>(),
                    It.IsAny<BigInteger>(),
                    It.IsAny<bool>())
                );

        nexusMoq.Setup( n => n.CreateSeries(
                    It.IsAny<StorageContext>(),
                    It.IsAny<IToken>(),
                    It.IsAny<BigInteger>(),
                    It.IsAny<BigInteger>(),
                    It.IsAny<TokenSeriesMode>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<ContractInterface>())
                ).Returns(
                    (StorageContext context,
                     IToken token,
                     BigInteger id,
                     BigInteger maxSupply,
                     TokenSeriesMode mode,
                     byte[] script,
                     ContractInterface abi) => 
                    {
                        return new TokenSeries(0, maxSupply, mode, script, abi, new byte[0]);
                    });

        nexusMoq.Setup( n => n.MintTokens(
                    It.IsAny<IRuntime>(),
                    It.IsAny<IToken>(),
                    It.IsAny<Address>(),
                    It.IsAny<Address>(),
                    It.IsAny<string>(),
                    It.IsAny<BigInteger>())
                )
            .Callback<IRuntime, IToken, Address, Address, string, BigInteger>(
                    (runtime, token, from, to, chain, amount) =>
                    {
                        this.Mints.Add(token.Symbol, amount);
                    });

        nexusMoq.Setup( n => n.HasGenesis()).Returns(true);

        nexusMoq.Setup( n => n.GetTokenInfo(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns(token);

        nexusMoq.Setup( n => n.TokenExists(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns(tokenExists);

        nexusMoq.Setup( n => n.GetChainByName(
                    It.IsAny<string>())
                ).Returns((string name) => 
                    {
                        if (string.IsNullOrEmpty(name))
                        {
                            return null;
                        }
                        else
                        {
                            return this.Chain;
                        }
                    });

        nexusMoq.Setup( n => n.GetParentChainByName(
                    It.IsAny<string>())
                ).Returns<IChain>(null);

        nexusMoq.Setup( n => n.GetProtocolVersion(
                    It.IsAny<StorageContext>())
                ).Returns(0);

        nexusMoq.Setup( n => n.CreateOrganization(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>()));

        if (!mockChain)
        {
            this.Chain = new Chain(nexusMoq.Object, "main");
        }
        else
        {
            var chainMoq = new Mock<IChain>();
            chainMoq.Setup( c => c.GetTransactionHashesForAddress(
                        It.IsAny<Address>())
                    ).Returns(new Hash[]
                        { 
                            Hash.FromString("0"),
                            Hash.FromString("1"),
                            Hash.FromString("2"),
                            Hash.FromString("3"),
                            Hash.FromString("4"),
                        });

            chainMoq.Setup( c => c.Nexus
                    ).Returns(nexusMoq.Object);

            chainMoq.Setup( c => c.GenerateUID(It.IsAny<StorageContext>())).Returns(1200);

            chainMoq.Setup( c => c.GetLastActivityOfAddress(It.IsAny<Address>())).Returns(new Timestamp(1601092859));

            chainMoq.Setup( c => c.GetNameFromAddress(It.IsAny<StorageContext>(), It.IsAny<Address>(), It.IsAny<Timestamp>())
                    ).Returns( (StorageContext context, Address address, Timestamp time) => 
                        {
                            return address.ToString();
                        });

            // set chain mock
            this.Chain = chainMoq.Object;
        }

        var runtime = new RuntimeVM(
                0,
                new byte[1] {0},
                0,
                this.Chain,
                this.Validator.Address,
                Timestamp.Now,
                tx,
                storage,
                null,
                ChainTask.Null,
                delayPayment
                );


        if (context is not null)
        {
            runtime.RegisterContext(token.Symbol, context);
            runtime.SetCurrentContext(context);
        }

        return runtime;
    }

    public InteropTests()
    {
        this.Mints = new ();
        this.TokenOwner = PhantasmaKeys.Generate();
        this.User1 = PhantasmaKeys.Generate();
        this.User2 = PhantasmaKeys.Generate();
        this.Validator = PhantasmaKeys.Generate();
        
        this.PartitionPath = Path.Combine(Path.GetTempPath(), "PhantasmaUnitTest", $"{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(this.PartitionPath);

        var maxSupply = 10000000;

        var ftFlags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        this.FungibleToken = new TokenInfo("EXX", "Example Token", TokenOwner.Address, 0, 8, ftFlags, new byte[1] { 0 }, new ContractInterface());

        var nftFlags = TokenFlags.Burnable | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        this.NonFungibleToken = new TokenInfo("EXNFT", "Example NFT", TokenOwner.Address, 0, 0, nftFlags, new byte[1] { 0 }, new ContractInterface());

        var ntFlags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable | TokenFlags.Stakable;
        this.NonTransferableToken = new TokenInfo("EXNT", "Example Token non transferable", TokenOwner.Address, 0, 8, ntFlags, new byte[1] { 0 }, new ContractInterface());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.PartitionPath, true);
        }
        catch (IOException)
        {
            Console.WriteLine("Unable to clean test directory");
        }

        this.Mints.Clear();
    }
}
*/
