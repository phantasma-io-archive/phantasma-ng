using System;
using Google.Protobuf;

namespace Tendermint.Extensions
{
    public static class IntExtensions
    {
        public static ByteString ToByteString(this int value)
        {
            var buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }

            return ByteString.CopyFrom(buffer, 0, buffer.Length);
        }
    }
}
