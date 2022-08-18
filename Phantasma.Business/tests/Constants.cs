using System.Collections.Generic;
using System.Numerics;
using Phantasma.Core;

namespace Phantasma.Business.Tests;

internal class Constants
{
    public static Address DefaultFromAddress = Address.FromText("P2KAPiHoaW4hp5b8wSUC1tojrihdRR56FU8tPoNVmNVDPYp");
    public static Address DefaultToAddress = Address.FromText("P2KA2x8P5sLfj75pAzQeSYA3QrFE2MzyV1WaHgVEcaEXGn6");

    public static byte[] TestTransferNftScript =>
        ScriptUtils.BeginScript().AllowGas(DefaultFromAddress, Address.Null, 100000, 6000).TransferNFT("GHOST",
                DefaultFromAddress, DefaultToAddress,
                BigInteger.Parse("80807712912753409015029052615541912663228133032695758696669246580757047529373"))
            .SpendGas(DefaultFromAddress).EndScript();

    public static byte[] TestContractCallScript =>
        ScriptUtils.BeginScript().AllowGas(DefaultFromAddress, DefaultToAddress, 100000, 6000).CallContract("TEST",
            "mintToken", new List<object>
            {
                1,
                1,
                1,
                "P2KA2x8P5sLfj75pAzQeSYA3QrFE2MzyV1WaHgVEcaEXGn6",
                10,
                "TEST",
                1,
                "Test NFT",
                "This is a test NFT.",
                1,
                "ipfs://bafybeidsqqsvffcqsxvq3h4gnwybt4gzshnmqm2ie4ui74db3gwnwp6vp4",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                0,
                "",
                0,
                "",
                0,
                false
            }.ToArray()).SpendGas(DefaultFromAddress).EndScript();
}
