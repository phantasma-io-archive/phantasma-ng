using System.Numerics;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Oracle;
using Phantasma.Core.Numerics;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM : GasMachine, IRuntime
{
    
    #region ORACLES

    // returns value in FIAT token
    public BigInteger GetTokenPrice(string symbol)
    {
        ExpectNameLength(symbol, nameof(symbol));

        if (symbol == DomainSettings.FiatTokenSymbol)
        {
            return UnitConversion.GetUnitValue(DomainSettings.FiatTokenDecimals);
        }

        Core.Throw.If(!Nexus.TokenExists(RootStorage, symbol), "cannot read price for invalid token");
        var token = GetToken(symbol);

        Core.Throw.If(Oracle == null, "cannot read price from null oracle");

        var value = Oracle.ReadPrice(Time, symbol);

        Expect(value >= 0, "token price not available for " + symbol);

        return value;
    }

    public byte[] ReadOracle(string URL)
    {
        ExpectUrlLength(URL, nameof(URL));
        return Oracle.Read<byte[]>(Time, URL);
    }
    #endregion

    
}
