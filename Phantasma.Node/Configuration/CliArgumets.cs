using Phantasma.Core.Utils;

namespace Phantasma.Node;

internal class CliArgumets
{
    public static Arguments Default { get; set; }

    public CliArgumets(string[] args)
    {
        Default = new Arguments(args);
    }
}
