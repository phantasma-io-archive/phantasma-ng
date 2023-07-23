using System;

namespace Phantasma.Core.Performance
{
    public struct ProfileMarker : IDisposable
    {
        private readonly string Name;
        private readonly ProfileSession Session;

        public ProfileMarker(string name)
        {
            Session = ProfileSession.CurrentSession;
            if(Session != null)
            {
                Name = name;
                Session.Push(name);
            }
            else
                Name = null;
        }

        public ProfileMarker(string name, ProfileMarker parent)
        {
            Session = parent.Session;
            if (Session != null)
            {
                Name = name;
                Session.Push(name);
            }
            else
                Name = null;
        }

        void IDisposable.Dispose()
        {
            if (Session != null)
            {
                Session.Pop(Name);
            }
        }
    }
}