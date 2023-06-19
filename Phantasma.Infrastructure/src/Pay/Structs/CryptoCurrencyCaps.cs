using System;

namespace Phantasma.Infrastructure.Pay;

[Flags]
public enum CryptoCurrencyCaps
{
    None = 0,
    Balance = 0x1,
    Transfer = 0x2,
    Stake = 0x4,
}
