using System.Collections.Generic;

namespace Phantasma.Business.Core.Nodes
{
    public abstract class ExpressionNode : CompilerNode
    {
        public ExpressionNode(CompilerNode owner) : base(owner)
        {
        }

        public abstract List<Instruction> Emit(Compiler compiler);

        public abstract TypeKind GetKind();
    }
}