using System.Collections.Generic;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Numerics;

namespace Phantasma.Core.Domain
{
    public static class DomainSettings
    {
        public const int LatestKnownProtocol = 14;

        public const int Phantasma20Protocol = 7;
        public const int Phantasma30Protocol = 8;

        public const int MaxTxPerBlock = 1024;

        public const int MaxOracleEntriesPerBlock = 5120;

        public const int MaxEventsPerBlock = 2048;

        public const int MaxEventsPerTx = 8096;
        
        public const int MaxTriggerLoop = 5;

        public const int MAX_TOKEN_DECIMALS = 18;
        
        public const int DefaultMinimumGasFee = 100000; // 100000
        public const int InitialValidatorCount = 4;

        public const string FuelTokenSymbol = "KCAL";
        public const string FuelTokenName = "Phantasma Energy";
        public const int FuelTokenDecimals = 10;

        public const string NexusMainnet = "mainnet";
        public const string NexusTestnet = "testnet";
        public const string NexusSimnet = "simnet";

        public const string StakingTokenSymbol = "SOUL";
        public const string StakingTokenName = "Phantasma Stake";
        public const int StakingTokenDecimals = 8;

        public const string FiatTokenSymbol = "USD";
        public const string FiatTokenName = "Dollars";
        public const int FiatTokenDecimals = 8;

        public const string RewardTokenSymbol = "CROWN";
        public const string RewardTokenName = "Phantasma Crown";
        
        public const string LiquidityTokenSymbol = "LP";
        public const string LiquidityTokenName = "Phantasma Liquidity";
        public const int LiquidityTokenDecimals = 8;

        public const string FuelPerContractDeployTag = "nexus.contract.cost";
        public const string FuelPerTokenDeployTag = "nexus.token.cost";
        public const string FuelPerOrganizationDeployTag = "nexus.organization.cost";

        public static readonly IEnumerable<string> SystemTokens = new List<string>
        {
                DomainSettings.FuelTokenSymbol,
                DomainSettings.StakingTokenSymbol,
                DomainSettings.FiatTokenSymbol,
                DomainSettings.RewardTokenSymbol,
                DomainSettings.LiquidityTokenSymbol
        };

        public const string RootChainName = "main";

        public const string ValidatorsOrganizationName = "validators";
        public const string MastersOrganizationName = "masters";
        public const string StakersOrganizationName = "stakers";

        public const string PhantomForceOrganizationName = "phantom_force";

        public static readonly BigInteger PlatformSupply = UnitConversion.ToBigInteger(100000000, FuelTokenDecimals);
        public const string PlatformName = "phantasma";

        public static readonly int ArchiveMinSize = 64; // in bytes
        public static readonly int ArchiveMaxSize = 104857600; //100mb
        public static readonly uint ArchiveBlockSize = MerkleTree.ChunkSize;

        public static readonly string InfusionName = "infusion";
        public static readonly Address InfusionAddress = SmartContract.GetAddressFromContractName(InfusionName);

        public const int NameMaxLength = 255;
        public const int UrlMaxLength = 2048;
        public const int ArgsMax = 64;
        public const int AddressMaxSize = 34;
        public const int ScriptMaxSize = short.MaxValue;
        public const int FieldMaxLength = 80;
        public const int FieldMinLength = 1;
    }
}
