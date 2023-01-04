// notes: Keep the structs here simple only using primitive C# types or arrays

using Phantasma.Core.Domain;

namespace Phantasma.Infrastructure.API
{
    public class BalanceResult
    {
        public string chain { get; set; }
        public string amount { get; set; }
        public string symbol { get; set; }
        public uint decimals { get; set; }
        public string[] ids { get; set; }
    }

    public class InteropResult
    {
        public string local { get; set; }
        public string external { get; set; }
    }

    public class PlatformResult
    {
        public string platform { get; set; }
        public string chain { get; set; }
        public string fuel { get; set; }
        public string[] tokens { get; set; }
        public InteropResult[] interop { get; set; }
    }

    public class GovernanceResult
    {
        public string name { get; set; }
        public string value { get; set; }
    }

    public class OrganizationResult
    {
        public string id { get; set; }
        public string name { get; set; }
        public string[] members { get; set; }
    }

    // TODO add APIDescription tags
    public class CrowdsaleResult
    {
        public string hash { get; set; }
        public string name { get; set; }
        public string creator { get; set; }
        public string flags { get; set; }
        public uint startDate { get; set; }
        public uint endDate { get; set; }
        public string sellSymbol { get; set; }
        public string receiveSymbol { get; set; }
        public uint price { get; set; }
        public string globalSoftCap { get; set; }
        public string globalHardCap { get; set; }
        public string userSoftCap { get; set; }
        public string userHardCap { get; set; }
    }

    public class NexusResult
    {
        [APIDescription("Name of the nexus")]
        public string name { get; set; }

        [APIDescription("Network protocol version")]
        public uint protocol { get; set; }

        [APIDescription("List of platforms")]
        public PlatformResult[] platforms { get; set; }

        [APIDescription("List of tokens")]
        public TokenResult[] tokens { get; set; }

        [APIDescription("List of chains")]
        public ChainResult[] chains { get; set; }

        [APIDescription("List of governance values")]
        public GovernanceResult[] governance { get; set; }

        [APIDescription("List of organizations")]
        public string[] organizations { get; set; }
    }

    public class StakeResult
    {
        [APIDescription("Amount of staked SOUL")]
        public string amount { get; set; }

        [APIDescription("Time of last stake")]
        public uint time { get; set; }

        [APIDescription("Amount of claimable KCAL")]
        public string unclaimed { get; set; }
    }

    public class StorageResult
    {
        [APIDescription("Amount of available storage bytes")]
        public uint available { get; set; }

        [APIDescription("Amount of used storage bytes")]
        public uint used { get; set; }

        [APIDescription("Avatar data")]
        public string avatar { get; set; }

        [APIDescription("List of stored files")]
        public ArchiveResult[] archives { get; set; }
    }

    public class AccountResult
    {
        public string address { get; set; }
        public string name { get; set; }

        [APIDescription("Info about staking if available")]
        public StakeResult stakes { get; set; }

        public string stake { get; set; } //Deprecated
        public string unclaimed { get; set; }//Deprecated

        [APIDescription("Amount of available KCAL for relay channel")]
        public string relay { get; set; }

        [APIDescription("Validator role")]
        public string validator { get; set; }

        [APIDescription("Info about storage if available")]
        public StorageResult storage { get; set; }

        public BalanceResult[] balances { get; set; }

        public string[] txs { get; set; }
    }

    public class LeaderboardRowResult
    {
        public string address { get; set; }
        public string value { get; set; }
    }

    public class LeaderboardResult
    {
        public string name { get; set; }
        public LeaderboardRowResult[] rows { get; set; }
    }

    public class DappResult
    {
        public string name { get; set; }
        public string address { get; set; }
        public string chain { get; set; }
    }

    public class ChainResult
    {
        public string name { get; set; }
        public string address { get; set; }

        [APIDescription("Name of parent chain")]
        public string parent { get; set; }

        [APIDescription("Current chain height")]
        public uint height { get; set; }

        [APIDescription("Chain organization")]
        public string organization { get; set; }

        [APIDescription("Contracts deployed in the chain")]
        public string[] contracts { get; set; }

        [APIDescription("Dapps deployed in the chain")]
        public string[] dapps { get; set; }
    }

    public class EventResult
    {
        public string address { get; set; }
        public string contract { get; set; }
        public string kind { get; set; }

        [APIDescription("Data in hexadecimal format, content depends on the event kind")]
        public string data { get; set; }
    }

    public class OracleResult
    {
        [APIDescription("URL that was read by the oracle")]
        public string url { get; set; }

        [APIDescription("Byte array content read by the oracle, encoded as hex string")]
        public string content { get; set; }
    }

    public class SignatureResult
    {
        [APIDescription("Kind of signature")]
        public string Kind { get; set; }

        [APIDescription("Byte array containing signature data, encoded as hex string")]
        public string Data { get; set; }
    }

    public class TransactionResult
    {
        [APIDescription("Hash of the transaction")]
        public string hash { get; set; }

        [APIDescription("Transaction chain address")]
        public string chainAddress { get; set; }

        [APIDescription("Block time")]
        public uint timestamp { get; set; }

        [APIDescription("Block height at which the transaction was accepted")]
        public int blockHeight { get; set; }

        [APIDescription("Hash of the block")]
        public string blockHash { get; set; }

        [APIDescription("Script content of the transaction, in hexadecimal format")]
        public string script { get; set; }

        [APIDescription("Payload content of the transaction, in hexadecimal format")]
        public string payload { get; set; }

        [APIDescription("List of events that triggered in the transaction")]
        public EventResult[] events { get; set; }

        [APIDescription("Result of the transaction, if any. Serialized, in hexadecimal format")]
        public string result { get; set; }

        [APIDescription("Fee of the transaction, in KCAL, fixed point")]
        public string fee { get; set; }

        [APIDescription("Executin state of the transaction")]
        public string state { get; set; }

        [APIDescription("List of signatures that signed the transaction")]
        public SignatureResult[] signatures { get; set; }

        [APIDescription("Sender of the transaction")]
        public string sender { get; set; }

        [APIDescription("Address to pay gas from")]
        public string gasPayer { get; set; }

        [APIDescription("Address used as gas target, if any")]
        public string gasTarget { get; set; }

        [APIDescription("The txs gas price")]
        public string gasPrice { get; set; }

        [APIDescription("The txs gas limit")]
        public string gasLimit { get; set; }

        [APIDescription("Expiration time of the transaction")]
        public uint expiration { get; set; }
    }

    public class AccountTransactionsResult
    {
        public string address { get; set; }

        [APIDescription("List of transactions")]
        public TransactionResult[] txs { get; set; }
    }

    public class PaginatedResult
    {
        public uint page { get; set; }
        public uint pageSize { get; set; }
        public uint total { get; set; }
        public uint totalPages { get; set; }

        public object result { get; set; }
    }

    public class BlockResult
    {
        public string hash { get; set; }

        [APIDescription("Hash of previous block")]
        public string previousHash { get; set; }

        public uint timestamp { get; set; }

        // TODO support bigint here
        public uint height { get; set; }

        [APIDescription("Address of chain where the block belongs")]
        public string chainAddress { get; set; }

        [APIDescription("Network protocol version")]
        public uint protocol { get; set; }

        [APIDescription("List of transactions in block")]
        public TransactionResult[] txs { get; set; }

        [APIDescription("Address of validator who minted the block")]
        public string validatorAddress { get; set; }

        [APIDescription("Amount of KCAL rewarded by this fees in this block")]
        public string reward { get; set; }

        [APIDescription("Block events")]
        public EventResult[] events { get; set; }

        [APIDescription("Block oracles")]
        public OracleResult[] oracles { get; set; }
    }

    public class TokenExternalResult
    {
        [APIDescription("Platform name")]
        public string platform { get; set; }

        [APIDescription("External hash")]
        public string hash { get; set; }
    }

    public class TokenPriceResult
    {
        public uint Timestamp { get; set; }
        public string Open { get; set; }
        public string High { get; set; }
        public string Low { get; set; }
        public string Close { get; set; }
    }

    public class TokenResult
    {
        [APIDescription("Ticker symbol for the token")]
        public string symbol { get; set; }

        public string name { get; set; }

        [APIDescription("Amount of decimals when converting from fixed point format to decimal format")]
        public int decimals { get; set; }

        [APIDescription("Amount of minted tokens")]
        public string currentSupply { get; set; }

        [APIDescription("Max amount of tokens that can be minted")]
        public string maxSupply { get; set; }

        [APIDescription("Total amount of burned tokens")]
        public string burnedSupply { get; set; }

        [APIDescription("Address of token contract")]
        public string address { get; set; }

        [APIDescription("Owner address")]
        public string owner { get; set; }

        public string flags { get; set; }

        [APIDescription("Script attached to token, in hex")]
        public string script { get; set; }

        [APIDescription("Series info. NFT only")]
        public TokenSeriesResult[] series { get; set; }

        [APIDescription("External platforms info")]
        public TokenExternalResult[] external { get; set; }

        [APIDescription("Cosmic swap historic data")]
        public TokenPriceResult[] price { get; set; }
    }

    public class TokenSeriesResult
    {
        public uint seriesID { get; set; }

        [APIDescription("Current amount of tokens in circulation")]
        public string currentSupply { get; set; }

        [APIDescription("Maximum possible amount of tokens")]
        public string maxSupply { get; set; }

        [APIDescription("Total amount of burned tokens")]
        public string burnedSupply { get; set; }

        public string mode { get; set; }

        public string script { get; set; }

        [APIDescription("List of methods")]
        public ABIMethodResult[] methods { get; set; }
    }

    public class TokenPropertyResult
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class TokenDataResult
    {
        [APIDescription("id of token")]
        public string ID { get; set; }

        [APIDescription("series id of token")]
        public string series { get; set; }

        [APIDescription("mint number of token")]
        public string mint { get; set; }

        [APIDescription("Chain where currently is stored")]
        public string chainName { get; set; }

        [APIDescription("Address who currently owns the token")]
        public string ownerAddress { get; set; }

        [APIDescription("Address who minted the token")]
        public string creatorAddress { get; set; }

        [APIDescription("Writable data of token, hex encoded")]
        public string ram { get; set; }

        [APIDescription("Read-only data of token, hex encoded")]
        public string rom { get; set; }

        [APIDescription("Status of nft")]
        public string status { get; set; }

        public TokenPropertyResult[] infusion { get; set; }

        public TokenPropertyResult[] properties { get; set; }
    }

    public class SendRawTxResult
    {
        [APIDescription("Transaction hash")]
        public string hash { get; set; }

        [APIDescription("Error message if transaction did not succeed")]
        public string error { get; set; }
    }

    public class AuctionResult
    {
        [APIDescription("Address of auction creator")]
        public string creatorAddress { get; set; }

        [APIDescription("Address of auction chain")]
        public string chainAddress { get; set; }
        public uint startDate { get; set; }
        public uint endDate { get; set; }
        public string baseSymbol { get; set; }
        public string quoteSymbol { get; set; }
        public string tokenId { get; set; }
        public string price { get; set; }
        public string endPrice { get; set; }
        public string extensionPeriod { get; set; }
        public string type { get; set; }
        public string rom { get; set; }
        public string ram { get; set; }
        public string listingFee { get; set; }
        public string currentWinner { get; set; }
    }

    public class ScriptResult
    {
        [APIDescription("List of events that triggered in the transaction")]
        public EventResult[] events { get; set; }

        public string result { get; set; } // deprecated

        [APIDescription("Results of the transaction, if any. Serialized, in hexadecimal format")]
        public string[] results { get; set; }

        [APIDescription("List of oracle reads that were triggered in the transaction")]
        public OracleResult[] oracles { get; set; }
    }

    public class ArchiveResult
    {
        [APIDescription("File name")]
        public string name { get; set; }

        [APIDescription("Archive hash")]
        public string hash { get; set; }

        [APIDescription("Time of creation")]
        public uint time { get; set; }

        [APIDescription("Size of archive in bytes")]
        public uint size { get; set; }

        [APIDescription("Encryption address")]
        public string encryption { get; set; }

        [APIDescription("Number of blocks")]
        public int blockCount { get; set; }

        [APIDescription("Missing block indices")]
        public int[] missingBlocks { get; set; }

        [APIDescription("List of addresses who own the file")]
        public string[] owners { get; set; }
    }

    public class ABIParameterResult
    {
        [APIDescription("Name of method")]
        public string name { get; set; }

        public string type { get; set; }
    }

    public class ABIMethodResult
    {
        [APIDescription("Name of method")]
        public string name { get; set; }

        public string returnType { get; set; }

        [APIDescription("Type of parameters")]
        public ABIParameterResult[] parameters { get; set; }
    }

    public class ABIEventResult
    {
        [APIDescription("Value of event")]
        public int value { get; set; }

        [APIDescription("Name of event")]
        public string name { get; set; }

        public string returnType { get; set; }

        [APIDescription("Description script (base16 encoded)")]
        public string description { get; set; }
    }

    public class ContractResult
    {
        [APIDescription("Name of contract")]
        public string name { get; set; }

        [APIDescription("Address of contract")]
        public string address { get; set; }

        [APIDescription("Script bytes, in hex format")]
        public string script { get; set; }

        [APIDescription("List of methods")]
        public ABIMethodResult[] methods { get; set; }

        [APIDescription("List of events")]
        public ABIEventResult[] events { get; set; }
    }

    public class ChannelResult
    {
        [APIDescription("Creator of channel")]
        public string creatorAddress { get; set; }

        [APIDescription("Target of channel")]
        public string targetAddress { get; set; }

        [APIDescription("Name of channel")]
        public string name { get; set; }

        [APIDescription("Chain of channel")]
        public string chain { get; set; }

        [APIDescription("Creation time")]
        public uint creationTime { get; set; }

        [APIDescription("Token symbol")]
        public string symbol { get; set; }

        [APIDescription("Fee of messages")]
        public string fee { get; set; }

        [APIDescription("Estimated balance")]
        public string balance { get; set; }

        [APIDescription("Channel status")]
        public bool active { get; set; }

        [APIDescription("Message index")]
        public int index { get; set; }
    }

    public class ReceiptResult
    {
        [APIDescription("Name of nexus")]
        public string nexus { get; set; }

        [APIDescription("Name of channel")]
        public string channel { get; set; }

        [APIDescription("Index of message")]
        public string index { get; set; }

        [APIDescription("Date of message")]
        public uint timestamp { get; set; }

        [APIDescription("Sender address")]
        public string sender { get; set; }

        [APIDescription("Receiver address")]
        public string receiver { get; set; }

        [APIDescription("Script of message, in hex")]
        public string script { get; set; }
    }

    public class PortResult
    {
        [APIDescription("Port description")]
        public string name { get; set; }

        [APIDescription("Port number")]
        public int port { get; set; }
    }

    public class PeerResult
    {
        [APIDescription("URL of peer")]
        public string url { get; set; }

        [APIDescription("Software version of peer")]
        public string version { get; set; }

        [APIDescription("Features supported by peer")]
        public string flags { get; set; }

        [APIDescription("Minimum fee required by node")]
        public string fee { get; set; }

        [APIDescription("Minimum proof of work required by node")]
        public uint pow { get; set; }

        [APIDescription("List of exposed ports")]
        public PortResult[] ports { get; set; }
    }

    public class ValidatorResult
    {
        [APIDescription("Address of validator")]
        public string address { get; set; }

        [APIDescription("Either primary or secondary")]
        public string type { get; set; }
    }

    // TODO document this
    public class SwapResult
    {
        public string sourcePlatform { get; set; }
        public string sourceChain { get; set; }
        public string sourceHash { get; set; }
        public string sourceAddress { get; set; }

        public string destinationPlatform { get; set; }
        public string destinationChain { get; set; }
        public string destinationHash { get; set; }
        public string destinationAddress { get; set; }

        public string symbol { get; set; }
        public string value { get; set; }
    }
}
