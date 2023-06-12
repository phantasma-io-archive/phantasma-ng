using System.IO;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Numerics;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Storage.Context.Structs
{
    public struct StorageKey : ISerializable
    {
        public byte[] keyData { get; private set; }

        public StorageKey(byte[] data)
        {
            this.keyData = data;
        }

        public override string ToString()
        {
            return Base16.Encode(keyData);
            //return ToHumanKey(keyData);
        }

        public override int GetHashCode()
        {
            return keyData.GetHashCode();
        }

        /*
        public static string ToHumanKey(byte[] key)
        {
            if (key.Length == Address.PublicKeyLength)
            {
                return new Address(key).Text;
            }

            if (key.Length > Address.PublicKeyLength)
            {
                var address = key.Take(Address.PublicKeyLength).ToArray();
                var temp = key.Skip(Address.PublicKeyLength).ToArray();

                var rest = DecodeKey(temp);
                if (rest != null)
                {
                    return $"{ToHumanKey(address)}.{rest}";
                }
            }

            {
                var rest = DecodeKey(key);
                if (rest != null)
                {
                    return rest;
                }
            }

            return "0x" + Base16.Encode(key);
        }*/

        public static bool IsASCII(byte[] key)
        {
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] < 32 || key[i] >= 127)
                {
                    return false;
                }
            }

            return true;
        }

        private static string DecodeKey(byte[] key)
        {
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] == (byte)'}')
                {
                    int index = i + 1;
                    var first = key.Take(index).ToArray();

                    if (IsASCII(first))
                    {
                        var name = global::System.Text.Encoding.ASCII.GetString(first);
                        if (name.StartsWith("{") && name.EndsWith("}"))
                        {
                            name = name.Substring(1, name.Length - 2);

                            if (i == key.Length - 1)
                            {
                                return name;
                            }

                            var temp = key.Skip(index).ToArray();

                            var rest = DecodeKey(temp);

                            if (rest == null)
                            {
                                return null;
                            }

                            return $"{name}.{rest}";
                        }
                    }

                    return null;
                }
                else if (key[0] == (byte)'<' && key[i] == (byte)'>')
                {
                    int index = i + 1;
                    var first = key.Take(index - 1).Skip(1).ToArray();

                    var num = new BigInteger(first);

                    var name = $"[{num}]";

                    if (i == key.Length - 1)
                    {
                        return name;
                    }

                    var temp = key.Skip(index).ToArray();

                    var rest = DecodeKey(temp);

                    if (rest == null)
                    {
                        return null;
                    }

                    return $"{name}{rest}";
                }
            }

            return null;
        }

        public static string ToHumanValue(byte[] key, byte[] value)
        {
            /*if (key.Length == Address.PublicKeyLength)
            {
                return new global::System.Numerics.BigInteger(value).ToString();
            }
            */
            return "0x" + Base16.Encode(value);
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteByteArray(keyData);
        }

        public void UnserializeData(BinaryReader reader)
        {
            keyData = reader.ReadByteArray();
        }
    }
}
