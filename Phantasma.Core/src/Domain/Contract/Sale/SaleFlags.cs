using System;

namespace Phantasma.Core.Domain.Contract.Sale;

[Flags]
public enum SaleFlags
{
    None = 0,
    Whitelist = 1,
}
