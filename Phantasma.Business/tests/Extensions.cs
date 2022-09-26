using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using System;
using System.Globalization;

namespace Phantasma.Blockchain.Tests;

public static class MockExtension
{
    public static Mock<IRuntime> SetupInvokeTriggerMoq(this Mock<IRuntime> mock, TriggerResult resultToken, TriggerResult resultAccount)
    {
        mock.Setup(r => r.InvokeTriggerOnToken(
                    It.IsAny<bool>(),
                    It.IsAny<IToken>(),
                    It.IsAny<TokenTrigger>(),
                    It.IsAny<object[]>())).Returns(resultToken);

        mock.Setup(r => r.InvokeTriggerOnAccount(
                    It.IsAny<bool>(),
                    It.IsAny<Address>(),
                    It.IsAny<AccountTrigger>(),
                    It.IsAny<object[]>())).Returns(resultAccount);

        return mock;
    }
}

public static class AssertExtension
{
    public static T ExpectException<T>(Func<object> action, string message) where T : Exception
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        string userMessage, finalMessage;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (!typeof(T).Equals(ex.GetType()))
            {
                finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    "Threw exception {2}, but {1} was expected. {0}\nException Message: {3}\n;Stack Trace: \n{4}",
                    "",
                    typeof(T).Name,
                ex.GetType().Name,
                ex.Message,
                ex.StackTrace);
                throw new AssertFailedException(string.Format(CultureInfo.CurrentCulture, "{0} failed. {1}", "Assert.ExpectException", finalMessage));
            }

            if (typeof(T).Equals(ex.GetType()))
            {
                if (!ex.Message.Equals(message))
                {
                    finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        "Threw exception {1}, but message differs, \n\texpected:\n\n\t{4}\n\n\treceived:\n\n\t{2}\n\nStack Trace: \n{3}",
                        typeof(T).Name,
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace,
                    message);
                    throw new AssertFailedException(string.Format(CultureInfo.CurrentCulture, "{0} failed. {1}", "Assert.ExpectException", finalMessage));
                }
            }

            return (T)ex;
        }

        // This will not hit, but need it for compiler.
        return null;
    }
}
