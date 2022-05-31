using System.Numerics;

namespace Phantasma.Core
{
    public enum TaskFrequencyMode
    {
        Always,
        Time,
        Blocks,
    }

    public enum TaskResult
    {
        Running,
        Halted,
        Crashed,
        Skipped,
    }
}

