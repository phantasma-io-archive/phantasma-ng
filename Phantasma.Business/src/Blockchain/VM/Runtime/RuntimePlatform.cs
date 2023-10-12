using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Interfaces;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM: GasMachine, IRuntime
{
    
    /*
    public void SetPlatformTokenHash(string symbol, string platform, Hash hash)
    {
        ExpectNameLength(symbol, nameof(symbol));
        ExpectNameLength(platform, nameof(platform));
        ExpectHashSize(hash, nameof(hash));

        Expect(this.IsRootChain(), "must be root chain");

        Expect(IsWitness(GenesisAddress), "invalid witness, must be genesis");

        Expect(platform != DomainSettings.PlatformName, "external token chain required");
        Expect(hash != Hash.Null, "hash cannot be null");

        var pow = Transaction.Hash.GetDifficulty();
        Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

        Expect(PlatformExists(platform), "platform not found");

        Expect(!string.IsNullOrEmpty(symbol), "token symbol required");
        Expect(ValidationUtils.IsValidTicker(symbol), "invalid symbol");
        //Expect(!TokenExists(symbol, platform), $"token {symbol}/{platform} already exists");

        Expect(!string.IsNullOrEmpty(platform), "chain name required");

        Nexus.SetPlatformTokenHash(symbol, platform, hash, RootStorage);
    }*/
    
    public bool PlatformExists(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.PlatformExists(RootStorage, name);
    }
    
    /*public BigInteger CreatePlatform(Address from, string name, string externalAddress, Address interopAddress, string fuelSymbol)
      {
          ExpectAddressSize(from, nameof(from));
          ExpectNameLength(name, nameof(name));
          ExpectUrlLength(externalAddress, nameof(externalAddress));
          ExpectAddressSize(interopAddress, nameof(interopAddress));
          ExpectNameLength(fuelSymbol, nameof(fuelSymbol));

          Expect(this.IsRootChain(), "must be root chain");

          Expect(from == GenesisAddress, "(CreatePlatform) must be genesis");
          Expect(IsWitness(from), "invalid witness");

          Expect(ValidationUtils.IsValidIdentifier(name), "invalid platform name");

          var platformID = Nexus.CreatePlatform(RootStorage, externalAddress, interopAddress, name, fuelSymbol);
          Expect(platformID > 0, $"creation of platform with id {platformID} failed");

          this.Notify(EventKind.PlatformCreate, from, name);
          return platformID;
      }*/
    
    public bool IsPlatformAddress(Address address)
    {
        ExpectAddressSize(address, nameof(address));
        return Nexus.IsPlatformAddress(RootStorage, address);
    }

    public void RegisterPlatformAddress(string platform, Address localAddress, string externalAddress)
    {
        ExpectNameLength(platform, nameof(platform));
        ExpectAddressSize(localAddress, nameof(localAddress));
        ExpectUrlLength(externalAddress, nameof(externalAddress));
        Expect(Chain.Name == DomainSettings.RootChainName, "must be in root chain");
        Nexus.RegisterPlatformAddress(RootStorage, platform, localAddress, externalAddress);
    }
    
    public IPlatform GetPlatformByName(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.GetPlatformInfo(RootStorage, name);
    }

    public IPlatform GetPlatformByIndex(int index)
    {
        index--;
        var platforms = GetPlatforms();
        if (index < 0 || index >= platforms.Length)
        {
            return null;
        }

        var name = platforms[index];
        return GetPlatformByName(name);
    }

    public string[] GetPlatforms()
    {
        return Nexus.GetPlatforms(RootStorage);
    }
    
}
