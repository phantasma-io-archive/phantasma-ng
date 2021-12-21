using System;
using System.Linq;
using System.Text;
using Google.Protobuf;

namespace Types.Extensions
{
    public static class ByteStringExtensions
    {
        public static int ToInt(this ByteString value)
        {
            var originalByteArray = value.ToByteArray();
            var byteArray = Enumerable
                .Repeat<Byte>(0, 4 - originalByteArray.Length)
                .Concat(originalByteArray)
                .ToArray();

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(byteArray);
            }

            return BitConverter.ToInt32(byteArray, 0);
        }

        public static string ToStringSafe(this ByteString value)
        {
            var byteArray = value.ToByteArray();
            var utf8String = Encoding.UTF8.GetString(byteArray);
            var decodedByteArray = Convert.FromBase64String(utf8String);
            return Encoding.UTF8.GetString(decodedByteArray);
        }
    }
}
