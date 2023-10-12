// notes: Keep the structs here simple only using primitive C# types or arrays

namespace Phantasma.Infrastructure.API.Structs
{
    public class BalanceResult
    {
        public string chain { get; set; }
        public string amount { get; set; }
        public string symbol { get; set; }
        public uint decimals { get; set; }
        public string[] ids { get; set; }
    }

    // TODO add APIDescription tags

    // TODO document this
}
