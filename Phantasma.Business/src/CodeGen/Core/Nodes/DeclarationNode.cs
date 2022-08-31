using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Business.CodeGen.Core.Nodes
{
    public class DeclarationNode : CompilerNode
    {
        public string identifier;
        public TypeNode type;

        public DeclarationNode(CompilerNode owner) : base(owner)
        {
            if (owner is BlockNode)
            {
                ((BlockNode)owner).declarations.Add(this);
            }
            else if (owner is ParameterNode)
            {
                ((ParameterNode)owner).decl = this;
            }
            else
            {
                throw new Exception("Invalid owner");
            }
        }

        public override IEnumerable<CompilerNode> Nodes => Enumerable.Empty<CompilerNode>();

        public override string ToString()
        {
            return base.ToString() + "=>" + this.identifier+"/"+this.type.Kind;
        }

    }
}