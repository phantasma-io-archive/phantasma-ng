using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Numerics;

using Phantasma.Simulator;
using Phantasma.Core.Cryptography;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Domain;

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
    }

}
