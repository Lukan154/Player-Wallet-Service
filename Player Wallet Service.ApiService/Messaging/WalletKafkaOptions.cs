namespace Player_Wallet_Service.ApiService.Messaging;

public sealed class WalletKafkaOptions
{
    public const string SectionName = "WalletKafka";

    public string TopicName { get; set; } = "wallet-events";
}
