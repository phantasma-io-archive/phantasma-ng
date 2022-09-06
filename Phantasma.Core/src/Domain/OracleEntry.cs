using System.Collections.Generic;

namespace Phantasma.Core.Domain
{
    public struct OracleEntry
    {
        public string URL { get; private set; }
        public byte[] Content { get; private set; }

        public OracleEntry(string url, byte[] content)
        {
            URL = url;
            Content = content;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is OracleEntry))
            {
                return false;
            }

            var entry = (OracleEntry)obj;
            return URL == entry.URL &&
                   EqualityComparer<object>.Default.Equals(Content, entry.Content);
        }

        public override int GetHashCode()
        {
            var hashCode = 1993480784;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(URL);
            hashCode = hashCode * -1521134295 + EqualityComparer<object>.Default.GetHashCode(Content);
            return hashCode;
        }
    }
}
