using System.Numerics;
using System.Collections.Generic;
using Phantasma.Shared.Types;

namespace Phantasma.Core;

public interface IOracleReader
{
    BigInteger ProtocolVersion { get; }
    IEnumerable<OracleEntry> Entries { get; }
    string GetCurrentHeight(string platformName, string chainName);
    void SetCurrentHeight(string platformName, string chainName, string height);
    List<InteropBlock> ReadAllBlocks(string platformName, string chainName);
    T Read<T>(Timestamp time, string url) where T : class;
    InteropTransaction ReadTransaction(string platform, string chain, Hash hash);
    void Clear();
    void MergeTxData();
}
