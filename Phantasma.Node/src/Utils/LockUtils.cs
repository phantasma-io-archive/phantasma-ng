using System.Collections.Concurrent;

namespace Phantasma.Node.Utils
{
    public class StringLocker
    {
        private readonly ConcurrentDictionary<string, object> _locks =
            new ConcurrentDictionary<string, object>();
    
        public object GetLockObject(string s)
        {
            return _locks.GetOrAdd(s, k => new object());
        }
    }

}
