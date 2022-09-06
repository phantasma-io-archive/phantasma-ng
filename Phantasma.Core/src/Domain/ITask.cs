namespace Phantasma.Core.Domain
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

