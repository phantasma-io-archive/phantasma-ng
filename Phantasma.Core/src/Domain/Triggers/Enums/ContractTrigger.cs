namespace Phantasma.Core.Domain.Triggers.Enums;

// ContractTrigger is also used for account scripts
public enum ContractTrigger
{
    OnMint, // address, symbol, amount
    OnBurn, // address, symbol, amount
    OnSend, // address, symbol, amount
    OnReceive, // address, symbol, amount
    OnWitness, // address
    OnUpgrade, // address
    OnMigrate, // from, to
    OnKill, // address
}
