using Phantasma.Business.VM.Utils;

namespace Phantasma.Business.CodeGen.Assembler
{
    internal class Label : Semanteme
    {
        public readonly string Name;

        public Label(uint lineNumber, string name) : base(lineNumber)
        {
            this.Name = name;
        }

        public override void Process(ScriptBuilder sb)
        {
            sb.EmitLabel(Name);
        }

        public override string ToString()
        {
            return Name;
        }

    }
}
