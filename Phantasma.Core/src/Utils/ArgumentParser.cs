using System;
using System.Collections.Generic;

namespace Phantasma.Core.Utils
{
    public class Arguments
    {
        private Dictionary<string, string> entries = new Dictionary<string, string>();
        private string defaultArgument;

        public Arguments(string[] args, string prefix = "-")
        {
            LoadValues(args, prefix);
        }

        private void LoadValues(string[] args, string prefix)
        {
            var lastIndex = args.Length - 1;
            for (int index = 0; index <= lastIndex; index++)
            {
                var arg = args[index];

                if (!arg.StartsWith(prefix))
                {
                    if (index == lastIndex)
                    {
                        defaultArgument = arg;
                        return;
                    }
                    else
                    {
                        throw new Exception("Invalid argument format: " + arg);
                    }
                }

                var temp = arg.Substring(prefix.Length).Split(new char[] { '=' }, 2);
                var key = temp[0];
                var val = temp.Length > 1 ? temp[1] : null;

                entries[key] = val;
            }
        }

        public string GetDefaultValue()
        {
            if (defaultArgument != null)
            {
                return defaultArgument;
            }

            throw new Exception("Not default argument found");
        }

        public string GetString(string key, string defaultVal = null, bool required = false)
        {
            string result;

            if (entries.ContainsKey(key))
            {
                result = entries[key];
            }
            else if (defaultVal != null)
            {
                result = defaultVal;
            }
            else
            {
                result = null;
            }

            if ((required && string.IsNullOrEmpty(result)) || (result != null && result.StartsWith("#")))
            {
                throw new Exception("Unconfigured setting: " + key);
            }

            if (result != null)
            {
                return result;
            }

            throw new Exception("Missing non-optional argument: " + key);
        }

        public int GetInt(string key, int defaultVal = 0)
        {
            var temp = GetString(key, defaultVal.ToString());
            int result;
            if (int.TryParse(temp, out result)) { return result; }
            return defaultVal;
        }

        public uint GetUInt(string key, uint defaultVal = 0)
        {
            var temp = GetString(key, defaultVal.ToString());
            uint result;
            if (uint.TryParse(temp, out result)) { return result; }
            return defaultVal;
        }

        public bool GetBool(string key, bool defaultVal)
        {
            var temp = GetString(key, defaultVal.ToString());
            bool result;
            if (bool.TryParse(temp, out result)) { return result; }
            return defaultVal;
        }

        public bool GetBool(string key)
        {
            var temp = GetString(key, null, true);
            return bool.Parse(temp);
        }

        public T GetEnum<T>(string key, T defaultVal) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException($"{typeof(T)} must be an enumerated type");
            }

            var temp = GetString(key, defaultVal.ToString());
            T result;
            if (Enum.TryParse<T>(temp, out result)) { return result; }
            return defaultVal;
        }

        public T GetEnum<T>(string key) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException($"{typeof(T)} must be an enumerated type");
            }

            var temp = GetString(key);
            return Enum.Parse<T>(temp);
        }

        public bool HasValue(string key)
        {
            return entries.ContainsKey(key);
        }		
    }
}
