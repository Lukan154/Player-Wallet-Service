using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Player_Wallet_Service.ApiService.Events;

namespace Player_Wallet_Service.ApiService.Messaging;

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

    public async Task PublishAsync(WalletEvent walletEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(walletEvent, JsonOptions);
            var message = new Message<string, string>
            {
                Key = walletEvent.PlayerId.ToString("D"),
                Value = payload
            };

            var result = await _producer.ProduceAsync(_options.TopicName, message, cancellationToken);

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
            _logger.LogError(
                ex,
                "Failed to publish {EventType} for player {PlayerId} to topic {Topic}",
                walletEvent.EventType,
                walletEvent.PlayerId,
                _options.TopicName);
        }
    }
}
