namespace Phantasma.Core.Domain.Triggers;

public enum TokenTrigger
{
    OnMint, // address, symbol, amount
    OnBurn, // address, symbol, amount
    OnSend, // address, symbol, amount
    OnReceive, // address, symbol, amount
    OnInfuse, // address, symbol, amount
    OnUpgrade, // address
    OnSeries, // address
    OnWrite, // address, data
    OnMigrate, // from, to
    OnKill, // address
}
