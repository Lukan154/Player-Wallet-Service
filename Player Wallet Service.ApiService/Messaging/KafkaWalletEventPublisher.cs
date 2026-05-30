using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Player_Wallet_Service.ApiService.Events;

namespace Player_Wallet_Service.ApiService.Messaging;

// Implements the IWalletEventPublisher interface to publish wallet events to a Kafka topic.

public sealed class KafkaWalletEventPublisher : IWalletEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> _producer;
    private readonly WalletKafkaOptions _options;
    private readonly ILogger<KafkaWalletEventPublisher> _logger;
    
    public KafkaWalletEventPublisher(
        IProducer<string, string> producer,
        IOptions<WalletKafkaOptions> options,
        ILogger<KafkaWalletEventPublisher> logger)
    {
        _producer = producer;
        _options = options.Value;
        _logger = logger;
    }

    // Publishes a wallet event to the configured Kafka topic.
    public async Task PublishAsync(WalletEvent walletEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Serialize the wallet event to JSON and create a Kafka message with the player ID as the key.
            var payload = JsonSerializer.Serialize(walletEvent, JsonOptions);
            var message = new Message<string, string>
            {
                Key = walletEvent.PlayerId.ToString("D"),
                Value = payload
            };

            var result = await _producer.ProduceAsync(_options.TopicName, message, cancellationToken);
            
            // Log the successful publication of the event, including the event type, player ID, topic, partition, and offset.
            _logger.LogInformation(
                "Published {EventType} for player {PlayerId} to {Topic} partition {Partition} offset {Offset}",
                walletEvent.EventType,
                walletEvent.PlayerId,
                _options.TopicName,
                result.Partition.Value,
                result.Offset.Value);
        }
        catch (Exception ex)
        {
            // If publishing to Kafka fails, the wallet operation has already been persisted, so the API still succeeds and the player is not impacted. 
            // However, the failure to publish the event is logged as an error for monitoring and troubleshooting purposes.
            _logger.LogError(
                ex,
                "Failed to publish {EventType} for player {PlayerId} to topic {Topic}",
                walletEvent.EventType,
                walletEvent.PlayerId,
                _options.TopicName);
        }
    }
}
