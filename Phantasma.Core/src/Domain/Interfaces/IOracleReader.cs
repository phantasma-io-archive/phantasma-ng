using System.Collections.Generic;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Contract.Interop;
using Phantasma.Core.Domain.Oracle;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Interfaces;

public interface IOracleReader
{
    BigInteger ProtocolVersion { get; }
    IEnumerable<OracleEntry> Entries { get; }
    string GetCurrentHeight(string platformName, string chainName);
    void SetCurrentHeight(string platformName, string chainName, string height);
    List<InteropBlock> ReadAllBlocks(string platformName, string chainName);
    T Read<T>(Timestamp time, string url) where T : class;
    InteropTransaction ReadTransaction(Timestamp time, string platform, string chain, Hash hash);
    void Clear();
    void MergeTxData();
    int GetMultiplier();
}