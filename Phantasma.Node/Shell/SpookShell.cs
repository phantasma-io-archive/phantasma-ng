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
        public Node Node { get; }

        public SpookShell(string[] args, Node node)
        {
            this.Node = node;
        }


    }
}
