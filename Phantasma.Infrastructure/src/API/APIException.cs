using System;

namespace Phantasma.Infrastructure.API
{
    public class APIException : Exception
    {
        public APIException(string msg) : base(msg)
        {
        }

        public APIException(string msg, Exception innerException) : base(msg, innerException)
        {
        }
    }
}
