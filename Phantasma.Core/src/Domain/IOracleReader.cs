using System.Numerics;
using System.Collections.Generic;
using Phantasma.Shared.Types;
using System.Threading.Tasks;

namespace Phantasma.Core;

public interface IOracleReader
{
    BigInteger ProtocolVersion { get; }
    IEnumerable<OracleEntry> Entries { get; }
    string GetCurrentHeight(string platformName, string chainName);
    void SetCurrentHeight(string platformName, string chainName, string height);
    List<InteropBlock> ReadAllBlocks(string platformName, string chainName);
    Task<T> Read<T>(Timestamp time, string url) where T : class;
    Task<InteropTransaction> ReadTransaction(string platform, string chain, Hash hash);
    void Clear();
    void MergeTxData();
}
