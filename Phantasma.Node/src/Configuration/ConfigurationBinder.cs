using System;
using Microsoft.Extensions.Configuration;

namespace Phantasma.Node.Configuration;

public static class ConfigurationBinder
{
    public static T GetValueEx<T>(this IConfiguration configuration, string key, T defaultValue = default(T)) where T : struct, IConvertible
    {
        if (typeof(T) == typeof(Int32))
        {
            // If command line arguments are initialized,
            // we try to get value from there, as it has a priority.
            if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
            {
                return (T)(object)CliArgumets.Default.GetInt(key);
            }

            // Otherwise we proceed with configuration's data.
            return (T)(object)configuration.GetValue<T>(key);
        }

        if (typeof(T) == typeof(UInt32))
        {
            // If command line arguments are initialized,
            // we try to get value from there, as it has a priority.
            if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
            {
                return (T)(object)CliArgumets.Default.GetUInt(key);
            }

            // Otherwise we proceed with configuration's data.
            return (T)(object)configuration.GetValue<T>(key);
        }

        if (typeof(T) == typeof(bool))
        {
            // If command line arguments are initialized,
            // we try to get value from there, as it has a priority.
            if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
            {
                return (T)(object)CliArgumets.Default.GetBool(key);
            }

            // Otherwise we proceed with configuration's data.
            return (T)(object)configuration.GetValue<T>(key);
        }

        if (typeof(T).IsEnum)
        {
            // If command line arguments are initialized,
            // we try to get value from there, as it has a priority.
            if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
            {
                var stringValue = CliArgumets.Default.GetString(key);
                Enum.Parse<T>(stringValue);
                return (T)(object)CliArgumets.Default.GetEnum<T>(key);
            }

            // Otherwise we proceed with configuration's data.
            return (T)(object)configuration.GetValue<T>(key);
        }

        throw new Exception($"Type {typeof(T)} is not supported");
    }
    public static string GetString(this IConfiguration configuration, string key, string defaultValue = null)
    {
        // If command line arguments are initialized,
        // we try to get value from there, as it has a priority.
        if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
        {
            return CliArgumets.Default.GetString(key);
        }

        // Otherwise we proceed with configuration's data.
        return configuration.GetValue<string>(key, defaultValue);
    }
}
