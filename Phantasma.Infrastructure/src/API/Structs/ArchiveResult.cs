namespace Phantasma.Infrastructure.API;

public class ArchiveResult
{
    [APIDescription("File name")]
    public string name { get; set; }

    [APIDescription("Archive hash")]
    public string hash { get; set; }

    [APIDescription("Time of creation")]
    public uint time { get; set; }

    [APIDescription("Size of archive in bytes")]
    public uint size { get; set; }

    [APIDescription("Encryption address")]
    public string encryption { get; set; }

    [APIDescription("Number of blocks")]
    public int blockCount { get; set; }

    [APIDescription("Missing block indices")]
    public int[] missingBlocks { get; set; }

    [APIDescription("List of addresses who own the file")]
    public string[] owners { get; set; }
}