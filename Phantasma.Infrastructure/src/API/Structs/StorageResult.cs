namespace Phantasma.Infrastructure.API.Structs;

public class StorageResult
{
    [APIDescription("Amount of available storage bytes")]
    public uint available { get; set; }

    [APIDescription("Amount of used storage bytes")]
    public uint used { get; set; }

    [APIDescription("Avatar data")]
    public string avatar { get; set; }

    [APIDescription("List of stored files")]
    public ArchiveResult[] archives { get; set; }
}