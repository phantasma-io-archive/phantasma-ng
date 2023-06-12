namespace Phantasma.Core.Domain.Contract.Interop;

public struct InteropNFT
{
    public readonly string Name;
    public readonly string Description;
    public readonly string ImageURL;

    public InteropNFT(string name, string description, string imageURL)
    {
        Name = name;
        Description = description;
        ImageURL = imageURL;
    }
}
