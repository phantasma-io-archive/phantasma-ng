using Xunit;

using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using Phantasma.Simulator;
using Phantasma.Core.Types;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Numerics;
using Phantasma.Business.VM.Utils;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Core.Storage.Context;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Infrastructure.Pay.Chains;

namespace Phantasma.LegacyTests
{
    public class ChainTests
    {
        [Fact]
        public void NullAddress()
        {
            var addr = Address.Null;
            Assert.True(addr.IsNull);
            Assert.True(addr.IsSystem);
            Assert.False(addr.IsUser);
            Assert.False(addr.IsInterop);

            Assert.True(Address.IsValidAddress(addr.Text));
        }

        [Fact]
        public void Decimals()
        {
            var places = 8;
            decimal d = 93000000;
            BigInteger n = 9300000000000000;

            var tmp1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(n, places), places);

            Assert.True(n == tmp1);
            Assert.True(d == UnitConversion.ToDecimal(UnitConversion.ToBigInteger(d, places), places));

            Assert.True(d == UnitConversion.ToDecimal(n, places));
            Assert.True(n == UnitConversion.ToBigInteger(d, places));

            var tmp2 = UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals);
            Assert.True(tmp2 > 0);

            decimal eos = 1006245120;
            var tmp3 = UnitConversion.ToBigInteger(eos, 18);
            var dec = UnitConversion.ToDecimal(tmp3, 18);
            Assert.True(dec == eos);

            BigInteger small = 60;
            var tmp4 = UnitConversion.ToDecimal(small, 10);
            var dec2 = 0.000000006m;
            Assert.True(dec2 == tmp4);
        }

        [Fact]
        public void GenesisBlock()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            Assert.True(nexus.HasGenesis());

            var genesisHash = nexus.GetGenesisHash(nexus.RootStorage);
            Assert.True(genesisHash != Hash.Null);

            var rootChain = nexus.RootChain;

            Assert.True(rootChain.Address.IsSystem);
            Assert.False(rootChain.Address.IsNull);

            var symbol = DomainSettings.FuelTokenSymbol;
            Assert.True(nexus.TokenExists(nexus.RootStorage, symbol));
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.True(token.MaxSupply == 0);

            var supply = nexus.RootChain.GetTokenSupply(rootChain.Storage, symbol);
            Assert.True(supply > 0);

            var balance = UnitConversion.ToDecimal(nexus.RootChain.GetTokenBalance(rootChain.Storage, token, owner.Address), DomainSettings.FuelTokenDecimals);
            Assert.True(balance > 0);

            Assert.True(rootChain != null);
            Assert.True(rootChain.Height > 0);

            /*var children = nexus.GetChildChainsByName(nexus.RootStorage, rootChain.Name);
            Assert.True(children.Any());*/

            Assert.True(nexus.IsPrimaryValidator(owner.Address, simulator.CurrentTime));

            var randomKey = PhantasmaKeys.Generate();
            Assert.False(nexus.IsPrimaryValidator(randomKey.Address, simulator.CurrentTime));

            /*var txCount = nexus.GetTotalTransactionCount();
            Assert.True(txCount > 0);*/

            simulator.TransferOwnerAssetsToAddress(randomKey.Address);
        }

        

        [Fact]
        public void CreateToken()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var accountChain = nexus.GetChainByName("account");
            var symbol = "BLA";

            var decimals = 18;

            var tokenSupply = UnitConversion.ToBigInteger(10000, decimals);
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "BlaToken", tokenSupply, decimals, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible);
            simulator.MintTokens(owner, owner.Address, symbol, tokenSupply);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var testUser = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);

            Assert.True(oldBalance > amount);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.True(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);

            Assert.True(transferBalance + newBalance == oldBalance);

            Assert.True(nexus.RootChain.IsContractDeployed(nexus.RootChain.Storage, symbol));

            // try call token contract method
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            {
                return new ScriptBuilder()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 999)
                .CallContract(symbol, "getDecimals")
                .SpendGas(owner.Address)
                .EndScript();
            });
            var block = simulator.EndBlock().FirstOrDefault();
            Assert.NotNull(block);

            var callResultBytes = block.GetResultForTransaction(tx.Hash);
            var callResult = Serialization.Unserialize<VMObject>(callResultBytes);
            var num = callResult.AsNumber();

            Assert.True(num == decimals);
        }

        [Fact]
        public void CreateNonDivisibleToken()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var symbol = "BLA";

            var tokenSupply = UnitConversion.ToBigInteger(100000000, 18);
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "BlaToken", tokenSupply, 0, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, owner.Address, symbol, tokenSupply);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var testUser = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);

            Assert.True(oldBalance > amount);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.True(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);

            Assert.True(transferBalance + newBalance == oldBalance);
        }
        
        [Fact]
        public void SimpleTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            var txA = simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            var txB = simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // Send from user A to user B
            simulator.BeginBlock();
            var txC = simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var hashes = simulator.Nexus.RootChain.GetTransactionHashesForAddress(testUserA.Address);
            Assert.True(hashes.Length == 3);
            Assert.True(hashes.Any(x => x == txA.Hash));
            Assert.True(hashes.Any(x => x == txB.Hash));
            Assert.True(hashes.Any(x => x == txC.Hash));

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            Assert.True(finalBalance == transferAmount);
        }

        [Fact]
        public void GenesisMigration()
        {
            var firstOwner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(firstOwner);
            var nexus = simulator.Nexus;

            var secondOwner = PhantasmaKeys.Generate();
            var testUser = PhantasmaKeys.Generate();
            var anotherTestUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(firstOwner, secondOwner.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.GenerateTransfer(firstOwner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var oldToken = nexus.GetTokenInfo(nexus.RootStorage, DomainSettings.RewardTokenSymbol);
            Assert.True(oldToken.Owner == firstOwner.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(firstOwner, ProofOfWork.None, () =>
            {
                return ScriptUtils.BeginScript().
                     AllowGas(firstOwner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit).
                     CallContract("account", "Migrate", firstOwner.Address, secondOwner.Address).
                     SpendGas(firstOwner.Address).
                     EndScript();
            });
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var newToken = nexus.GetTokenInfo(nexus.RootStorage, DomainSettings.RewardTokenSymbol);
            Assert.True(newToken.Owner == secondOwner.Address);

            simulator.BeginBlock(secondOwner); // here we change the validator keys in simulator
            simulator.GenerateTransfer(secondOwner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // inflation check
            simulator.TimeSkipDays(91);
            simulator.BeginBlock(); 
            simulator.GenerateTransfer(testUser, anotherTestUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var crownBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.RewardTokenSymbol, firstOwner.Address);
            Assert.True(crownBalance == 0);

            crownBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.RewardTokenSymbol, secondOwner.Address);
            Assert.True(crownBalance == 1);

            var thirdOwner = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(secondOwner, ProofOfWork.None, () =>
            {
                return ScriptUtils.BeginScript().
                     AllowGas(secondOwner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit).
                     CallContract("account", "Migrate", secondOwner.Address, thirdOwner.Address).
                     SpendGas(secondOwner.Address).
                     EndScript();
            });
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            simulator.BeginBlock(thirdOwner); // here we change the validator keys in simulator
            simulator.GenerateTransfer(thirdOwner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());
        }

        [Fact]
        public void SystemAddressTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            var txA = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            var txB = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var hashes = simulator.Nexus.RootChain.GetTransactionHashesForAddress(testUser.Address);
            Assert.True(hashes.Length == 2);
            Assert.True(hashes.Any(x => x == txA.Hash));
            Assert.True(hashes.Any(x => x == txB.Hash));

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.True(finalBalance == transferAmount);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            Assert.True(finalBalance == transferAmount);
        }

        
        /*
        [Fact]
        public void ChainSwapIn()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var neoKeys = NeoKeys.Generate();

            var limit = 800;

            // 1 - at this point a real NEO transaction would be done to the NEO address obtained from getPlatforms in the API
            // here we just use a random hardcoded hash and a fake oracle to simulate it
            var swapSymbol = "GAS";
            var neoTxHash = OracleSimulator.SimulateExternalTransaction("neo", NeoWallet.NeoID, neoKeys.PublicKey, neoKeys.Address, swapSymbol, 2);

            var tokenInfo = nexus.GetTokenInfo(nexus.RootStorage, swapSymbol);

            // 2 - transcode the neo address and settle the Neo transaction on Phantasma
            var transcodedAddress = Address.FromKey(neoKeys);

            var testUser = PhantasmaKeys.Generate();

            var platformName = NeoWallet.NeoPlatform;
            var platformChain = NeoWallet.NeoPlatform;

            var gasPrice = simulator.MinimumFee;

            Func<decimal, byte[]> genScript = (fee) =>
            {
                return new ScriptBuilder()
                .CallContract("interop", "SettleTransaction", transcodedAddress, platformName, platformChain, neoTxHash)
                .CallContract("swap", "SwapFee", transcodedAddress, swapSymbol, UnitConversion.ToBigInteger(fee, DomainSettings.FuelTokenDecimals))
                .TransferBalance(swapSymbol, transcodedAddress, testUser.Address)
                .AllowGas(transcodedAddress, Address.Null, gasPrice, limit)
                .TransferBalance(DomainSettings.FuelTokenSymbol, transcodedAddress, testUser.Address)
                .SpendGas(transcodedAddress).EndScript();
            };

            // note the 0.1m passed here could be anything else. It's just used to calculate the actual fee
            var vm = new GasMachine(genScript(0.1m), 0, null);
            var result = vm.Execute();
            var usedGas = UnitConversion.ToDecimal((int)(vm.UsedGas * gasPrice), DomainSettings.FuelTokenDecimals);

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(neoKeys, ProofOfWork.None, () =>
            {
                return genScript(usedGas);
            });

            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var swapToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, swapSymbol);
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, swapToken, transcodedAddress);
            Assert.True(balance == 0);

            balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, swapToken, testUser.Address);
            Assert.True(balance > 0);

            var settleHash = (Hash)nexus.RootChain.InvokeContract(nexus.RootStorage, "interop", nameof(InteropContract.GetSettlement), "neo", neoTxHash).ToObject();
            Assert.True(settleHash == tx.Hash);

            var fuelToken = nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            var leftoverBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, transcodedAddress);
            Assert.True(leftoverBalance == 0);
        }*/

        [Fact(Skip = "Later when chain is live add this feature")]
        // TODO: .
        public void ChainSwapOut()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var rootChain = nexus.RootChain;

            var testUser = PhantasmaKeys.Generate();

            var potAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);
            
            simulator.InitPlatforms();

            // 0 - just send some assets to the 
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals));
            //simulator.GenerateTransfer(owner, potAddress, nexus.RootChain, "GAS", UnitConversion.ToBigInteger(10, 8));
            //simulator.MintTokens(owner, potAddress, "GAS", UnitConversion.ToBigInteger(1, 8));
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var oldBalance = rootChain.GetTokenBalance(rootChain.Storage, DomainSettings.StakingTokenSymbol, testUser.Address);
            var oldSupply = rootChain.GetTokenSupply(rootChain.Storage, DomainSettings.StakingTokenSymbol);

            // 1 - transfer to an external interop address
            var targetAddress = NeoWallet.EncodeAddress("AG2vKfVpTozPz2MXvye4uDCtYcTnYhGM8F");
            simulator.BeginBlock();
            simulator.GenerateTransfer(testUser, targetAddress, nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var currentBalance = rootChain.GetTokenBalance(rootChain.Storage, DomainSettings.StakingTokenSymbol, testUser.Address);
            var currentSupply = rootChain.GetTokenSupply(rootChain.Storage, DomainSettings.StakingTokenSymbol);

            Assert.True(currentBalance < oldBalance);
            Assert.True(currentBalance == 0);

            Assert.True(currentSupply < oldSupply);
        }

        [Fact]
        public void QuoteConversions()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            nexus.CreatePlatform(nexus.RootStorage, "", Address.Null, "neo", "gas");
            Assert.True(nexus.PlatformExists(nexus.RootStorage, "neo"));
            Assert.True(nexus.TokenExists(nexus.RootStorage, "NEO"));

            var context = new StorageChangeSetContext(nexus.RootStorage);
            var runtime = new RuntimeVM(-1, new byte[0], 0, nexus.RootChain, Address.Null, Timestamp.Now, Transaction.Null, context, new OracleSimulator(nexus), ChainTask.Null);

            var temp = runtime.GetTokenQuote("NEO", "KCAL", 1);
            var price = UnitConversion.ToDecimal(temp, DomainSettings.FuelTokenDecimals);
            Assert.True(price == 100);

            temp = runtime.GetTokenQuote("KCAL", "NEO", UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals));
            price = UnitConversion.ToDecimal(temp, 0);
            Assert.True(price == 1);

            temp = runtime.GetTokenQuote("SOUL", "KCAL", UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
            price = UnitConversion.ToDecimal(temp, DomainSettings.FuelTokenDecimals);
            Assert.True(price == 5);
        }


        [Fact(Skip = "Sidechains not in use ")]
        public void SideChainTransferDifferentAccounts()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;

            var symbol = DomainSettings.FuelTokenSymbol;

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.True(sideAmount > 0);

            // Send from Genesis address to "sender" user
            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            simulator.BeginBlock();
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, "main", "test");
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var targetChain = nexus.GetChainByName("test");

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.True(balance == originalAmount);

            var crossFee = UnitConversion.ToBigInteger(0.001m, token.Decimals);

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, receiver.Address, targetChain, sideAmount, crossFee);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // finish the chain transfer
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(receiver, nexus.RootChain, targetChain, txA);
            Assert.True(simulator.LastBlockWasSuccessful());

            // verify balances
            var feeB = targetChain.GetTransactionFee(txB);
            balance = targetChain.GetTokenBalance(targetChain.Storage, token, receiver.Address);
            var expectedAmount = (sideAmount + crossFee) - feeB;
            Assert.True(balance == expectedAmount);

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA + crossFee);

            balance = sourceChain.GetTokenBalance(sourceChain.Storage, token, sender.Address);
            Assert.True(balance == leftoverAmount);
        }

        [Fact(Skip = "Sidechains not in use ")]
        public void SideChainTransferSameAccount()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;

            var symbol = DomainSettings.FuelTokenSymbol;

            var sender = PhantasmaKeys.Generate();

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            var originalAmount = UnitConversion.ToBigInteger(1, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.True(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            simulator.BeginBlock();
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, "main", "test");
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var targetChain = nexus.GetChainByName("test");

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.True(balance == originalAmount);

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, sender.Address, targetChain, sideAmount, 0);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // finish the chain transfer from parent to child
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(sender, sourceChain, targetChain, txA);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // verify balances
            var feeB = targetChain.GetTransactionFee(txB);
            balance = targetChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            //Assert.True(balance == sideAmount - feeB); TODO CHECK THIS BERNARDO

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA);

            balance = sourceChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.True(balance == leftoverAmount);

            sideAmount /= 2;
            simulator.BeginBlock();
            var txC = simulator.GenerateSideChainSend(sender, symbol, targetChain, sender.Address, sourceChain, sideAmount, 0);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // finish the chain transfer from child to parent
            simulator.BeginBlock();
            var txD = simulator.GenerateSideChainSettlement(sender, targetChain, sourceChain, txC);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());
        }

        [Fact(Skip = "Sidechains not in use ")]
        public void SideChainTransferMultipleSteps()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            simulator.BeginBlock();
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, nexus.RootChain.Name, "sale");
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var sourceChain = nexus.RootChain;
            var sideChain = nexus.GetChainByName("sale");
            Assert.True(sideChain != null);

            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.True(sideAmount > 0);

            var newChainName = "testing";

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, sideChain.Name, newChainName);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var targetChain = nexus.GetChainByName(newChainName);

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.True(balance == originalAmount);

            // do a side chain send using test user balance from root to apps chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, sender.Address, sideChain, sideAmount, 0);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // finish the chain transfer
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(sender, nexus.RootChain, sideChain, txA);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var txCostA = simulator.Nexus.RootChain.GetTransactionFee(txA);
            var txCostB = sideChain.GetTransactionFee(txB);
            sideAmount = sideAmount - txCostA;

            balance = sideChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Console.WriteLine($"{balance}/{sideAmount}");
            Assert.True(balance == sideAmount);

            var extraFree = UnitConversion.ToBigInteger(0.01m, token.Decimals);

            sideAmount -= extraFree * 10;

            // do another side chain send using test user balance from apps to target chain
            simulator.BeginBlock();
            var txC = simulator.GenerateSideChainSend(sender, symbol, sideChain, receiver.Address, targetChain, sideAmount, extraFree);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var appSupplies = new SupplySheet(symbol, sideChain, nexus);
            var childBalance = appSupplies.GetChildBalance(sideChain.Storage, targetChain.Name);
            var expectedChildBalance = sideAmount + extraFree;

            // finish the chain transfer
            simulator.BeginBlock();
            var txD = simulator.GenerateSideChainSettlement(receiver, sideChain, targetChain, txC);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // TODO  verify balances
        }


        [Fact]
        public void NoGasSameChainTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;
            simulator.GetFundsInTheFuture(owner);

            var accountChain = nexus.GetChainByName("account");

            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(10, token.Decimals);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.True(oldBalance == amount);

            var gasFee = nexus.RootChain.GetTransactionFee(tx);
            Assert.True(gasFee > 0);

            amount /= 2;
            simulator.BeginBlock();
            simulator.GenerateTransfer(sender, receiver.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, receiver.Address);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);

            Assert.True(transferBalance + newBalance + gasFee == oldBalance);

            // create a new receiver
            receiver = PhantasmaKeys.Generate();

            //Try to send the entire balance without being able to afford fees from sender to receiver
            simulator.BeginBlock();
            tx = simulator.GenerateTransfer(sender, receiver.Address, nexus.RootChain, symbol, transferBalance);
            simulator.EndBlock();
            Assert.False(simulator.LastBlockWasSuccessful());

            // verify balances, receiver should have 0 balance
            transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, receiver.Address);
            Assert.True(transferBalance == 0, "Transaction failed completely as expected");
        }

        [Fact(Skip = "Sidechains not in use ")]
        public void NoGasTestSideChainTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            simulator.BeginBlock();
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, nexus.RootChain.Name, "sale");
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.GetChainByName("sale");

            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.True(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.True(balance == originalAmount);

            Transaction txA = null, txB = null;

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, receiver.Address, targetChain, originalAmount, 1);
            simulator.EndBlock();
            Assert.False(simulator.LastBlockWasSuccessful());

            var blockAHash = nexus.RootChain.GetLastBlockHash();
            var blockA = nexus.RootChain.GetBlockByHash(blockAHash);

            // finish the chain transfer
            simulator.BeginBlock();
            txB = simulator.GenerateSideChainSettlement(sender, nexus.RootChain, targetChain, txA);
            simulator.EndBlock();
            Assert.False(simulator.LastBlockWasSuccessful());

            // verify balances, receiver should have 0 balance
            balance = targetChain.GetTokenBalance(simulator.Nexus.RootStorage, token, receiver.Address);
            Assert.True(balance == 0);
        }


        [Fact]
        public void AddressComparison()
        {
            var owner = PhantasmaKeys.FromWIF("Kweyrx8ypkoPfzMsxV4NtgH8vXCWC1s1Dn3c2KJ4WAzC5nkyNt3e");
            var expectedAddress = owner.Address.Text;

            var input = "P2K9LSag1D7EFPBvxMa1fW1c4oNbmAQX7qj6omvo17Fwrg8";
            var address = Address.FromText(input);

            Assert.True(expectedAddress == input);

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var genesisAddress = simulator.CurrentValidatorAddresses.FirstOrDefault();
            Assert.True(address == genesisAddress);
            Assert.True(address.Text == genesisAddress.Text);
            Assert.True(address.ToByteArray().SequenceEqual(genesisAddress.ToByteArray()));
        }

        [Fact]
        public void ChainTransferExploit()
        {
            var owner = PhantasmaKeys.FromWIF("L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25");

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var user = PhantasmaKeys.Generate();

            var symbol = DomainSettings.StakingTokenSymbol;

            var chainAddressStr = Base16.Encode(simulator.Nexus.RootChain.Address.ToByteArray());
            var userAddressStr = Base16.Encode(user.Address.ToByteArray());

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, user.Address, simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, 10000000000);
            simulator.GenerateTransfer(owner, user.Address, simulator.Nexus.RootChain, DomainSettings.StakingTokenSymbol, 100000000);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var chainAddress = simulator.Nexus.RootChain.Address;
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, chainAddress, simulator.Nexus.RootChain, symbol, 100000000);

            var block = simulator.EndBlock().FirstOrDefault();
            Assert.NotNull(block);
            Assert.True(simulator.LastBlockWasSuccessful());

            var evts = block.GetEventsForTransaction(tx.Hash);
            Assert.True(evts.Any(x => x.Kind == EventKind.TokenReceive && x.Address == chainAddress));

            var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, symbol);

            var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, chainAddress);
            Assert.True(initialBalance > 10000);

            string[] scriptString = new string[]
            {
                $"alias r5, $sourceAddress",
                $"alias r6, $targetAddress",
                $"alias r7, $amount",
                $"alias r8, $symbol",

                $"load $amount, 10000",
                $@"load $symbol, ""{symbol}""",

                $"load r11 0x{chainAddressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop $sourceAddress",

                $"load r11 0x{userAddressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop $targetAddress",

                $"push $amount",
                $"push $symbol",
                $"push $targetAddress",
                $"push $sourceAddress",
                "extcall \"Runtime.TransferTokens\"",
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(user.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit).
                    EmitRaw(script).
                    SpendGas(user.Address).
                    EndScript());
            simulator.EndBlock();
            Assert.False(simulator.LastBlockWasSuccessful());

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, simulator.Nexus.RootChain.Address);
            Assert.True(initialBalance == finalBalance);
        }

        [Fact]
        public void TransactionFees()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // Send from user A to user B
            simulator.BeginBlock();
            simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            var block = simulator.EndBlock().FirstOrDefault();
            Assert.True(simulator.LastBlockWasSuccessful());

            Assert.True(block != null);

            var hash = block.TransactionHashes.First();

            var feeValue = nexus.RootChain.GetTransactionFee(hash);
            var feeAmount = UnitConversion.ToDecimal(feeValue, DomainSettings.FuelTokenDecimals);
            Assert.True(feeAmount >= 0.0009m, $"{feeAmount} >= 0.0009m");

            var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserB.Address);
            Assert.True(finalBalance == transferAmount);
        }

        [Fact(Skip = "Tendermint tests not supported yet")]
        public void ValidatorSwitch()
        {
            var owner = PhantasmaKeys.Generate();
            var owner2 = PhantasmaKeys.Generate();
            var owner3 = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(new []{owner, owner2, owner3});
            var nexus = simulator.Nexus;

            simulator.blockTimeSkip = TimeSpan.FromSeconds(5);

            var secondValidator = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var stakeAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            // make first validator allocate 5 more validator spots       
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, secondValidator.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, secondValidator.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 999).
                    CallContract(NativeContractKind.Governance, nameof(GovernanceContract.SetValue), owner.Address, ValidatorContract.ValidatorSlotsTag, 5).
                    SpendGas(owner.Address).
                    EndScript());
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            // make second validator candidate stake enough to become a stake master
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(secondValidator, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(secondValidator.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit).
                    CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), secondValidator.Address, stakeAmount).
                    SpendGas(secondValidator.Address).
                    EndScript());
            simulator.EndBlock();

            // set a second validator, no election required because theres only one validator for now
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit).
                    CallContract(NativeContractKind.Validator, nameof(ValidatorContract.SetValidator), secondValidator.Address, 1, ValidatorType.Primary).
                    SpendGas(owner.Address).
                    EndScript());
            var block = simulator.EndBlock().First();
            Assert.True(simulator.LastBlockWasSuccessful());

            // verify that we suceed adding a new validator
            var events = block.GetEventsForTransaction(tx.Hash).ToArray();
            Assert.True(events.Length > 0);
            Assert.True(events.Any(x => x.Kind == EventKind.ValidatorPropose));

            // make the second validator accept his spot
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(secondValidator, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().
                AllowGas(secondValidator.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit).
                CallContract(NativeContractKind.Validator, "SetValidator", secondValidator.Address, 1, ValidatorType.Primary).
                SpendGas(secondValidator.Address).
                EndScript());
            block = simulator.EndBlock().First();

            // verify that we suceed electing a new validator
            events = block.GetEventsForTransaction(tx.Hash).ToArray();
            Assert.True(events.Length > 0);
            Assert.True(events.Any(x => x.Kind == EventKind.ValidatorElect));

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var validatorSwitchAttempts = 100;
            var transferAmount = UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals);
            var accountBalance = transferAmount * validatorSwitchAttempts;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();
            
            var currentValidatorIndex = 0;

            var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            for (int i = 0; i < validatorSwitchAttempts; i++)
            {
                var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserB.Address);

                // here we skip to a time where its supposed to be the turn of the given validator index

                SkipToValidatorIndex(simulator, currentValidatorIndex);
                //simulator.CurrentTime = (DateTime)simulator.Nexus.GenesisTime + TimeSpan.FromSeconds(120 * 500 + 130);

                //TODO needs to be checked again
                //var currentValidator = currentValidatorIndex == 0 ? owner : secondValidator;
                // .GetValidator(simulator.Nexus.RootStorage, simulator.CurrentTime)
                var currentValidator = (simulator.Nexus.RootChain.ValidatorAddress == owner.Address) ? owner : secondValidator;

                simulator.BeginBlock(currentValidator);
                simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
                var lastBlock = simulator.EndBlock().First();

                var firstTxHash = lastBlock.TransactionHashes.First();
                events = lastBlock.GetEventsForTransaction(firstTxHash).ToArray();
                Assert.True(events.Length > 0);
                //Assert.True(events.Any(x => x.Kind == EventKind.ValidatorSwitch));

                var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserB.Address);
                Assert.True(finalBalance == initialBalance + transferAmount);

                currentValidatorIndex = currentValidatorIndex == 1 ? 0 : 1; //toggle the current validator index
            }

            // Send from user A to user B
            // NOTE this block is baked by the second validator
            
        }

        private void SkipToValidatorIndex(NexusSimulator simulator, int i)
        {
            uint skippedSeconds = 0;
            var genesisBlock = simulator.Nexus.GetGenesisBlock();
            DateTime genesisTime = genesisBlock.Timestamp;
            var diff = (simulator.CurrentTime - genesisTime).Seconds;
            //var index = (int)(diff / 120) % 2;
            skippedSeconds = (uint)(120 - diff);
            //Console.WriteLine("index: " + index);

            //while (index != i)
            //{
            //    skippedSeconds++;
            //    diff++;
            //    index = (int)(diff / 120) % 2;
            //}

            Console.WriteLine("skippedSeconds: " + skippedSeconds);
            simulator.CurrentTime = simulator.CurrentTime.AddSeconds(skippedSeconds);
        }

        [Fact]
        public void GasFeeCalculation()
        {
            var limit = 400;
            var testUser = PhantasmaKeys.Generate();
            var transcodedAddress = PhantasmaKeys.Generate().Address;
            var swapSymbol = "SOUL";

            var script = new ScriptBuilder()
            .CallContract("interop", "SettleTransaction", transcodedAddress, "neo", "neo", Hash.Null)
            .CallContract("swap", "SwapFee", transcodedAddress, swapSymbol, UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals))
            .TransferBalance(swapSymbol, transcodedAddress, testUser.Address)
            .AllowGas(transcodedAddress, Address.Null, Transaction.DefaultGasLimit, limit)
            .SpendGas(transcodedAddress).EndScript();

            var vm = new GasMachine(script, 0, null);
            var result = vm.Execute();
            Assert.True(result == ExecutionState.Halt);
            Assert.True(vm.UsedGas > 0);
        }

        [Fact]
        public void ChainTransferStressTest()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            simulator.blockTimeSkip = TimeSpan.FromSeconds(5);

            var testUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals);
            var stakeAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            for (int i = 0; i < 1000; i++)
            {
                var target = PhantasmaKeys.Generate();
                simulator.GenerateTransfer(owner, target.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1);
            }
            simulator.EndBlock();

            var x = 0;

            for (int i = 0; i < 1000; i++)
            {
                var target = PhantasmaKeys.Generate();
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, 1);
                simulator.EndBlock();
            }

            x = 0;
        }

        [Fact]
        public void DeployCustomAccountScript()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            simulator.blockTimeSkip = TimeSpan.FromSeconds(5);

            var testUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals);
            var stakeAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            string[] scriptString;

            string message = "customEvent";
            var addressStr = Base16.Encode(testUser.Address.ToByteArray());

            var onMintTrigger = AccountTrigger.OnMint.ToString();
            var onWitnessTrigger = AccountTrigger.OnWitness.ToString();

            scriptString = new string[]
            {
                $"alias r4, $triggerMint",
                $"alias r5, $triggerWitness",
                $"alias r6, $comparisonResult",
                $"alias r8, $currentAddress",
                $"alias r9, $sourceAddress",

                $"@{onWitnessTrigger}: NOP ",
                $"pop $currentAddress",
                $"load r11 0x{addressStr}",
                $"push r11",
                "extcall \"Address()\"",
                $"pop $sourceAddress",
                $"equal $sourceAddress, $currentAddress, $comparisonResult",
                $"jmpif $comparisonResult, @end",
                $"load r0 \"something failed\"",
                $"throw r0",

                $"@{onMintTrigger}: NOP",
                $"pop $currentAddress",
                $"load r11 0x{addressStr}",
                $"push r11",
                $"extcall \"Address()\"",
                $"pop r11",

                $"load r10, {(int)EventKind.Custom}",
                $@"load r12, ""{message}""",

                $"push r10",
                $"push r11",
                $"push r12",
                $"extcall \"Runtime.Event\"",

                $"@end: ret"
            };

            DebugInfo debugInfo;
            Dictionary<string, int> labels;
            var script = AssemblerUtils.BuildScript(scriptString, "test", out debugInfo, out labels);

            var triggerList = new[] { AccountTrigger.OnWitness, AccountTrigger.OnMint };

            // here we fetch the jump offsets for each trigger
            var triggerMap = new Dictionary<AccountTrigger, int>();
            foreach (var trigger in triggerList)
            {
                var triggerName = trigger.ToString();
                var offset = labels[triggerName];
                triggerMap[trigger] = offset;
            }

            // now with that, we can build an usable contract interface that exposes those triggers as contract calls
            var methods = AccountContract.GetTriggersForABI(triggerMap);
            var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
            var abiBytes = abi.ToByteArray();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None,
                () => ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .CallContract("account", "RegisterScript", testUser.Address, script, abiBytes)
                    .SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();
        }

        [Fact]
        public void DeployCustomContract()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            simulator.blockTimeSkip = TimeSpan.FromSeconds(5);

            var testUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(8000, DomainSettings.FuelTokenDecimals);
            var stakeAmount = StakeContract.DefaultMasterThreshold;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            string[] scriptString;

            var methodName = "sum";

            scriptString = new string[]
            {
                $"@{methodName}: NOP ",
                $"pop r1",
                $"pop r2",
                $"add r1 r2 r3",
                $"push r3",
                $"@end: ret",
                $"@onUpgrade: ret",
                $"@onKill: ret",
            };

            DebugInfo debugInfo;
            Dictionary<string, int> labels;
            var script = AssemblerUtils.BuildScript(scriptString, "test", out debugInfo, out labels);

            var methods = new[]
            {
                new ContractMethod(methodName , VMType.Number, labels[methodName], new []{ new ContractParameter("a", VMType.Number), new ContractParameter("b", VMType.Number) }),
                new ContractMethod("onUpgrade", VMType.None, labels["onUpgrade"], new []{ new ContractParameter("addr", VMType.Object) }),
                new ContractMethod("onKill", VMType.None, labels["onKill"], new []{ new ContractParameter("addr", VMType.Object) }),
            };

            var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
            var abiBytes = abi.ToByteArray();

            var contractName = "test";

            // deploy it
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.Minimal,
                () => ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .CallInterop("Runtime.DeployContract", testUser.Address, contractName, script, abiBytes)
                    .SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            // send some funds to contract address
            var contractAddress = SmartContract.GetAddressFromContractName(contractName);
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, contractAddress, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();


            // now stake some SOUL on the contract address
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None,
                () => ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), contractAddress, UnitConversion.ToBigInteger(5, DomainSettings.StakingTokenDecimals))
                    .SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            // upgrade it
            var newScript = Core.Utils.ByteArrayUtils.ConcatBytes(script, new byte[] { (byte)Opcode.NOP }); // concat useless opcode just to make it different
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.Minimal,
                () => ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .CallInterop("Runtime.UpgradeContract", testUser.Address, contractName, newScript, abiBytes)
                    .SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            // kill it
            Assert.True(nexus.RootChain.IsContractDeployed(nexus.RootStorage, contractName));
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.Minimal,
                () => ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .CallInterop("Runtime.KillContract", testUser.Address, contractName)
                    .SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            Assert.False(nexus.RootChain.IsContractDeployed(nexus.RootStorage, contractName));
        }

        [Fact]
        public void Inflation()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            // skip 3 months to reach next inflation date
            simulator.TimeSkipDays(90);

            // we need to generate at least one block more to trigger inflation tx
            Block block = simulator.TimeSkipDays(1);

            var inflation = false;
            foreach(var tx in block.TransactionHashes)
            {
                var events = block.GetEventsForTransaction(tx);
                foreach (var evt in events)
                {
                    if (evt.Kind == EventKind.Inflation)
                    {
                        inflation = true;
                    }
                }
            }

            Assert.True(inflation);
        }

    }

}
