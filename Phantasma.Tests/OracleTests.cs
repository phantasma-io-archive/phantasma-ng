using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Numerics;

using Phantasma.Simulator;
using Phantasma.Core.Cryptography;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Domain;
using Phantasma.Business.VM.Utils;

namespace Phantasma.LegacyTests
{
    [TestClass]
    public class OracleTests
    {
        [TestMethod]
        public void OracleTestNoData()
        {
            var owner = PhantasmaKeys.Generate();
            var wallet = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            //for (var i = 0; i < 65536; i++)
            for (var i = 0; i < 100; i++)
            {
                var url = DomainExtensions.GetOracleBlockURL("neo", "neoEmpty", new BigInteger(i));
                var iBlock = nexus.GetOracleReader().Read<InteropBlock>(DateTime.Now, url);
            }

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, wallet.Address, nexus.RootChain as Chain, "SOUL", 100);
            var block = simulator.EndBlock().First();

            Assert.IsTrue(block.OracleData.Count() == 0);
            Console.WriteLine("block oracle data: " + block.OracleData.Count());

        }

        [TestMethod]
        public void OracleTestWithData()
        {
            var owner = PhantasmaKeys.Generate();
            var wallet = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            //for (var i = 0; i < 65536; i++)
            for (var i = 0; i < 100; i++)
            {
                var url = DomainExtensions.GetOracleBlockURL("neo", "neo", new BigInteger(i));
                var iBlock = nexus.GetOracleReader().Read<InteropBlock>(DateTime.Now, url);
            }

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, wallet.Address, nexus.RootChain as Chain, "SOUL", 100);
            var block = simulator.EndBlock().First();

            Console.WriteLine("block oracle data: " + block.OracleData.Count());
            Assert.IsTrue(block.OracleData.Count() == 100);
        }

        [TestMethod]
        public void OracleTestWithTooMuchData()
        {
            var owner = PhantasmaKeys.Generate();
            var wallet = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            for (int i = 0; i < DomainSettings.MaxOracleEntriesPerBlock + 1; i++)
            {
                var url = DomainExtensions.GetOracleBlockURL("neo", "neo", new BigInteger(i));
                var iBlock = nexus.GetOracleReader().Read<InteropBlock>(DateTime.Now, url);
            }

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, wallet.Address, nexus.RootChain as Chain, "SOUL", 100);
                simulator.EndBlock().First();
            });
        }


        [TestMethod]
        public void OraclePrice()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "SOUL")
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "SOUL")
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 9997)
                    .SpendGas(owner.Address)
                    .EndScript());
            var block = simulator.EndBlock().First();

            foreach (var txHash in block.TransactionHashes)
            {
                var blkResult = block.GetResultForTransaction(txHash);
                var vmObj = VMObject.FromBytes(blkResult);
                Console.WriteLine("price: " + vmObj);
            }

            //TODO finish test
        }

        [TestMethod]
        public void OracleData()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "SOUL")
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "KCAL")
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .SpendGas(owner.Address)
                    .EndScript());
            var block1 = simulator.EndBlock().First();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "SOUL")
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "KCAL")
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                    .SpendGas(owner.Address)
                    .EndScript());
            var block2 = simulator.EndBlock().First();

            var oData1 = block1.OracleData.Count();
            var oData2 = block2.OracleData.Count();

            Console.WriteLine("odata1: " + oData1);
            Console.WriteLine("odata2: " + oData2);

            Assert.IsTrue(oData1 == oData2);
        }

    }

}
