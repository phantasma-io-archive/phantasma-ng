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
