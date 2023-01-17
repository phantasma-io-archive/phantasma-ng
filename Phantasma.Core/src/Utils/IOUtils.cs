using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;

namespace Phantasma.Core.Utils
{
    public static class IOUtils
    {
        public static void WriteVarInt(this BinaryWriter writer, long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException();
            if (value < 0xFD)
            {
                writer.Write((byte)value);
            }
            else if (value <= 0xFFFF)
            {
                writer.Write((byte)0xFD);
                writer.Write((ushort)value);
            }
            else if (value <= 0xFFFFFFFF)
            {
                writer.Write((byte)0xFE);
                writer.Write((uint)value);
            }
            else
            {
                writer.Write((byte)0xFF);
                writer.Write(value);
            }
        }

        public static void WriteBigInteger(this BinaryWriter writer, BigInteger n)
        {
            var bytes = n.ToSignedByteArray();
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
        
        public static void WriteTimestamp(this BinaryWriter writer, Timestamp timestamp)
        {
            writer.Write(timestamp.Value); // UInt32
        }

        public static void WriteByteArray(this BinaryWriter writer, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                writer.WriteVarInt(0);
                return;
            }
            writer.WriteVarInt(bytes.Length);
            writer.Write(bytes);
        }

        public static void WriteVarString(this BinaryWriter writer, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                writer.Write((byte)0);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            writer.WriteVarInt(bytes.Length);
            writer.Write(bytes);
        }
        
        public static ulong ReadVarInt(this BinaryReader reader, ulong max = ulong.MaxValue)
        {
            byte fb = reader.ReadByte();
            ulong value;
            if (fb == 0xFD)
                value = reader.ReadUInt16();
            else if (fb == 0xFE)
                value = reader.ReadUInt32();
            else if (fb == 0xFF)
                value = reader.ReadUInt64();
            else
                value = fb;
            if (value > max) throw new FormatException();
            return value;
        }

        public static BigInteger ReadBigInteger(this BinaryReader reader)
        {
            var length = reader.ReadByte();
            var bytes = reader.ReadBytes(length);
            return new BigInteger(bytes);
        }
        
        public static Timestamp ReadTimestamp(this BinaryReader reader)
        {
            var value = reader.ReadUInt32();
            return new Timestamp(value);
        }

        public static byte[] ReadByteArray(this BinaryReader reader)
        {
            var length = (int)reader.ReadVarInt();
            if (length == 0)
                return new byte[0];

            var bytes = reader.ReadBytes(length);
            return bytes;
        }

        public static string ReadVarString(this BinaryReader reader)
        {
            var length = (int)reader.ReadVarInt();
            if (length == 0)
                return null;
            var bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        public static string ReadVarString(this BinaryReader reader, int max)
        {
            return Encoding.UTF8.GetString(reader.ReadVarBytes(max));
        }

        public static byte[] ReadVarBytes(this BinaryReader reader, int max = 0x1000000)
        {
            return reader.ReadFixedBytes((int)reader.ReadVarInt((ulong)max));
        }

        public static byte[] ReadFixedBytes(this BinaryReader reader, int size)
        {
            var index = 0;
            var data = new byte[size];

            while (size > 0)
            {
                var bytesRead = reader.Read(data, index, size);

                if (bytesRead <= 0)
                {
                    throw new FormatException();
                }

                size -= bytesRead;
                index += bytesRead;
            }

            return data;
        }

        public static int GetVarSize(int value)
        {
            if (value < 0xFD)
                return sizeof(byte);
            else if (value <= 0xFFFF)
                return sizeof(byte) + sizeof(ushort);
            else
                return sizeof(byte) + sizeof(uint);
        }

        public static int GetVarSize(this string value)
        {
            int size = Encoding.UTF8.GetByteCount(value);
            return GetVarSize(size) + size;
        }

        public static int GetVarSize<T>(this IReadOnlyCollection<T> value)
        {
            int value_size;
            Type t = typeof(T);
            if (typeof(ISerializable).IsAssignableFrom(t))
            {
                value_size = value.OfType<ISerializable>().Sum(p => p.Serialize().Length);
            }
            else if (t.GetTypeInfo().IsEnum)
            {
                int element_size;
                Type u = t.GetTypeInfo().GetEnumUnderlyingType();
                if (u == typeof(sbyte) || u == typeof(byte))
                    element_size = 1;
                else if (u == typeof(short) || u == typeof(ushort))
                    element_size = 2;
                else if (u == typeof(int) || u == typeof(uint))
                    element_size = 4;
                else //if (u == typeof(long) || u == typeof(ulong))
                    element_size = 8;
                value_size = value.Count * element_size;
            }
            else
            {
                value_size = value.Count * Marshal.SizeOf<T>();
            }
            return GetVarSize(value.Count) + value_size;
        }

        public static void Write(this BinaryWriter writer, ISerializable value)
        {
            value.SerializeData(writer);
        }

        public static void WriteNullableArray<T>(this BinaryWriter writer, T[] value) where T : class, ISerializable
        {
            writer.WriteVarInt(value.Length);
            foreach (var item in value)
            {
                bool isNull = item is null;
                writer.Write(!isNull);
                if (isNull) continue;
                item.SerializeData(writer);
            }
        }

        public static void Write<T>(this BinaryWriter writer, IReadOnlyCollection<T> value) where T : ISerializable
        {
            writer.WriteVarInt(value.Count);
            foreach (T item in value)
            {
                item.SerializeData(writer);
            }
        }

        public static T[] ReadSerializableArray<T>(this BinaryReader reader, int max = 0x1000000) where T : ISerializable, new()
        {
            T[] array = new T[reader.ReadVarInt((ulong)max)];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new T();
                array[i].UnserializeData(reader);
            }
            return array;
        }
        
        public static T ReadSerializable<T>(this BinaryReader reader, int max = 0x1000000) where T : ISerializable, new()
        {
            T item = new T();
            item.UnserializeData(reader);
            return item;
        }

        public static byte[] ToArray(this ISerializable value)
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true);
            value.SerializeData(writer);
            writer.Flush();
            return ms.ToArray();
        }

        public static void WriteVarBytes(this BinaryWriter writer, ReadOnlySpan<byte> value)
        {
            writer.WriteVarInt(value.Length);
            writer.Write(value);
        }
    }
}
