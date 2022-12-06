using Xunit;

using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Infrastructure.API;
using TransactionResult = Phantasma.Core.Domain.TransactionResult;

namespace Phantasma.Business.Tests.Blockchain;

// TODO: Make this work
public class ApiTests
{
    public class TestData
    {
        public PhantasmaKeys owner;
        public Nexus nexus;
        public NexusSimulator simulator;
        //public  API => NexusAPI.; NexusAPI
    }

    private static readonly string testWIF = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25";
    private static readonly string testAddress = "P2K6Sm1bUYGsFkxuzHPhia1AbANZaHBJV54RgtQi5q8oK34";

    /*private TestData CreateAPI(bool useMempool = false)
    {
        var owner = PhantasmaKeys.FromWIF(testWIF);
        var nexus = new Nexus("simnet", null, null);
        nexus.SetOracleReader(new OracleSimulator(nexus));
        var sim = new NexusSimulator(nexus, owner, 1234);
        var mempool = useMempool? new Mempool(sim.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes("TEST"), 0, new DummyLogger()) : null;
        mempool?.SetKeys(owner);

        var api = new NexusAPI(sim.Nexus);
        api.Mempool = mempool;

        var data = new TestData()
        {
            owner = owner,
            simulator = sim,
            nexus = sim.Nexus,
            api = api
        };

        mempool?.StartInThread();

        return data;
    }*/

    /*[Fact]
    public void TestGetAccountValid()
    {
        var test = CreateAPI();

        var temp = test.api.GetAccount(testAddress);
        var account = (AccountResult)temp;
        Assert.True(account.address == testAddress);
        Assert.True(account.name == "genesis");
        Assert.True(account.balances.Length > 0);
    }

    [Fact]
    public void TestGetBlockAndTransaction()
    {
        var test = CreateAPI();

        var genesisHash = test.nexus.GetGenesisHash(test.nexus.RootStorage);

        var genesisBlockHash = genesisHash.ToString();

        var temp = test.api.GetBlockByHash(genesisBlockHash);
        var block = (BlockResult)temp;
        Assert.True(block.hash == genesisBlockHash);
        Assert.True(block.height == 1);

        var genesisTxHash = block.txs.FirstOrDefault().hash;

        temp = NexusAPI.GetTransaction(genesisTxHash);
        var tx = (TransactionResult)temp;
        Assert.True(tx.hash == genesisTxHash);
        Assert.True(tx.blockHeight == 1);
        Assert.True(tx.blockHash == genesisBlockHash);
    }

    [Fact]
    public void TestMultipleCallsOneRequest()
    {
        var test = CreateAPI();

        var randomKey = PhantasmaKeys.Generate();

        var script = new ScriptBuilder().
            CallContract("account", "LookUpAddress", test.owner.Address).
            CallContract("account", "LookUpAddress", randomKey.Address).
            EndScript();

        var temp = NexusAPI.InvokeRawScript("main", Base16.Encode(script));
        var scriptResult = (ScriptResult)temp;
        Assert.True(scriptResult.results.Length == 2);

        var names = scriptResult.results.Select(x => Base16.Decode(x)).Select(bytes => Serialization.Unserialize<VMObject>(bytes)).Select(obj => obj.AsString()).ToArray();
        Assert.True(names.Length == 2);
        Assert.True(names[0] == "genesis");
        Assert.True(names[1] == ValidationUtils.ANONYMOUS_NAME);
    }

    [Fact]
    public void TestGetAccountInvalidAddress()
    {
        var test = CreateAPI();

        var result = (ErrorResult)NexusAPI.GetAccount("blabla");
        Assert.True(!string.IsNullOrEmpty(result.error));
    }

    //TODO doesn't really make sense, vm  throws a contract not found, not sure what should be tested here, revisit later
    //[Fact]
    //public void TestTransactionError()
    //{
    //    var test = CreateAPI(true);

    //    var contractName = "blabla";
    //    var script = new ScriptBuilder().CallContract(contractName, "bleble", 123).ToScript();

    //    var chainName = DomainSettings.RootChainName;
    //    test.simulator.CurrentTime = Timestamp.Now;
    //    var tx = new Transaction("simnet", chainName, script, test.simulator.CurrentTime + TimeSpan.FromHours(1), "UnitTest");
    //    tx.Sign(PhantasmaKeys.FromWIF(testWIF));
    //    var txBytes = tx.ToByteArray(true);
    //    var temp = NexusAPI.SendRawTransaction(Base16.Encode(txBytes));
    //    var result = (SingleResult)temp;
    //    Assert.True(result.value != null);
    //    var hash = result.value.ToString();
    //    Assert.True(hash == tx.Hash.ToString());

    //    var startTime = DateTime.Now;
    //    do
    //    {
    //        var timeDiff = DateTime.Now - startTime;
    //        if (timeDiff.Seconds > 20)
    //        {
    //            throw new Exception("Test timeout");
    //        }

    //        var status = NexusAPI.GetTransaction(hash);
    //        if (status is ErrorResult)
    //        {
    //            var error = (ErrorResult)status;
    //            var msg = error.error.ToLower();
    //            if (msg != "pending")
    //            {
    //                Assert.True(msg.Contains(contractName));
    //                break;
    //            }
    //        }
    //    } while (true);
    //}

    [Fact]
    public void TestGetAccountNFT()
    {
        var test = CreateAPI();

        var chain = test.nexus.RootChain;

        var symbol = "COOL";

        var testUser = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        test.simulator.BeginBlock();
        test.simulator.GenerateToken(test.owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
        test.simulator.EndBlock();

        var token = test.simulator.Nexus.GetTokenInfo(test.simulator.Nexus.RootStorage, symbol);
        Assert.True(test.simulator.Nexus.TokenExists(test.simulator.Nexus.RootStorage, symbol), "Can't find the token symbol");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        var simulator = test.simulator;
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(test.owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var account = (AccountResult)NexusAPI.GetAccount(testUser.Address.Text);
        Assert.True(account.address == testUser.Address.Text);
        Assert.True(account.name == ValidationUtils.ANONYMOUS_NAME);
        Assert.True(account.balances.Length == 1);

        var balance = account.balances[0];
        Assert.True(balance.symbol == symbol);
        Assert.True(balance.ids.Length == 1);

        var info = (TokenDataResult)NexusAPI.GetNFT(symbol, balance.ids[0], true);
        Assert.True(info.ID == balance.ids[0]);
        var tokenStr = Base16.Encode(tokenROM);
        Assert.True(info.rom == tokenStr);
    }

    [Fact]
    public void TestGetABIFunction()
    {
        var test = CreateAPI();

        var result = (ContractResult)NexusAPI.GetContract(test.nexus.RootChain.Name, "exchange");

        var methodCount = typeof(ExchangeContract).GetMethods();

        var method = methodCount.FirstOrDefault(x => x.Name == "GetOrderBook");

        Assert.True(method != null);

        var parameters = method.GetParameters();

        Assert.True(parameters.Length == 3);
        Assert.True(parameters.Count(x => x.ParameterType == typeof(string)) == 2);
        Assert.True(parameters.Count(x => x.ParameterType == typeof(ExchangeOrderSide)) == 1);

        var returnType = method.ReturnType;

        Assert.True(returnType == typeof(ExchangeOrder[]));
    }


    [Fact]
    public void TestGetABIMethod()
    {
        var test = CreateAPI();

        var result = (ContractResult)NexusAPI.GetNexus().GetContractByName(test.nexus.RootChain.Name, "exchange");

        var methodCount = typeof(ExchangeContract).GetMethods();

        var method = methodCount.FirstOrDefault(x => x.Name == "OpenMarketOrder");

        Assert.True(method != null);

        var parameters = method.GetParameters();

        Assert.True(parameters.Length == 6);
        Assert.True(parameters.Count(x => x.ParameterType == typeof(string)) == 2);
        Assert.True(parameters.Count(x => x.ParameterType == typeof(ExchangeOrderSide)) == 1);
        Assert.True(parameters.Count(x => x.ParameterType == typeof(BigInteger)) == 1);
        Assert.True(parameters.Count(x => x.ParameterType == typeof(Address)) == 2);

        var returnType = method.ReturnType;

        Assert.True(returnType == typeof(void));
    }*/
}
