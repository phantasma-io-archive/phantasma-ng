namespace Phantasma.Business.VM;

public struct DebugRange
{
    public readonly uint SourceLine;
    public readonly int StartOffset;
    public readonly int EndOffset;

    public DebugRange(uint sourceLine,  int startOffset, int endOffset)
    {
        SourceLine = sourceLine;
        StartOffset = startOffset;
        EndOffset = endOffset;
    }

    public override string ToString()
    {
        return $"Line {SourceLine} => {StartOffset} : {EndOffset}";
    }
}
