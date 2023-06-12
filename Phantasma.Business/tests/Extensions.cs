using Moq;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Triggers;

namespace Phantasma.Business.Tests;

public static class MockExtension
{
    public static Mock<IRuntime> SetupInvokeTriggerMoq(this Mock<IRuntime> mock, TriggerResult resultToken, TriggerResult resultAccount)
    {
        mock.Setup(r => r.InvokeTriggerOnToken(
                    It.IsAny<bool>(),
                    It.IsAny<IToken>(),
                    It.IsAny<TokenTrigger>(),
                    It.IsAny<object[]>())).Returns(resultToken);

        mock.Setup(r => r.InvokeTriggerOnContract(
                    It.IsAny<bool>(),
                    It.IsAny<Address>(),
                    It.IsAny<ContractTrigger>(),
                    It.IsAny<object[]>())).Returns(resultAccount);

        return mock;
    }
}
