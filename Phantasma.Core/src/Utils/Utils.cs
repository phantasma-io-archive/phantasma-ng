using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Phantasma.Core.Utils;

public static class Utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToUInt32(this byte[] value, int startIndex)
    {
        var a = value[startIndex];
        startIndex++;
        var b = value[startIndex];
        startIndex++;
        var c = value[startIndex];
        startIndex++;
        var d = value[startIndex];
        startIndex++;
        return (uint)(a + (b << 8) + (c << 16) + (d << 24));
    }

    public static string ToJsonString(this JsonDocument jdoc)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        jdoc.WriteTo(writer);
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
