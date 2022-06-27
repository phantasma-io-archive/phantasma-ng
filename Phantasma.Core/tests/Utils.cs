using System.IO;

namespace Phantasma.Core.Tests;

public static class Utils
{
    public static string GetStringFromByteArray(byte[] array)
    {
        using var stream = new MemoryStream(array);
        using var streamReader = new StreamReader(stream);
        return streamReader.ReadToEnd();
    }
}
