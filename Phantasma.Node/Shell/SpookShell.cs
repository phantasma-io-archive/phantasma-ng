using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Node.Utils;

namespace Phantasma.Node.Shell
{
    class SpookShell
    {
        private Node _node;

        public SpookShell(string[] args, Node node)
        {
            _node = node;
        }


    }
}
