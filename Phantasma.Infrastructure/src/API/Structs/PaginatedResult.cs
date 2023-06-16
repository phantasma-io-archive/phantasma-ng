namespace Phantasma.Infrastructure.API.Structs;

public class PaginatedResult
{
    public uint page { get; set; }
    public uint pageSize { get; set; }
    public uint total { get; set; }
    public uint totalPages { get; set; }

    public object result { get; set; }
}