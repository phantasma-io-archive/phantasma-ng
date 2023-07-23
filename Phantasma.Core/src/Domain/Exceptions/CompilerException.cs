using System;

namespace Phantasma.Core.Domain.Exceptions
{
    public class CompilerException : Exception
    {
        public CompilerException(uint lineNumber, string message)
            : base($"ERROR: {message} in line {lineNumber}.")
        {
        }
    }
}
