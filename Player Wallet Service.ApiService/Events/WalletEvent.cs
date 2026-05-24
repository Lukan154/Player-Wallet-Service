namespace Player_Wallet_Service.ApiService.Events;

public sealed record WalletEvent
{
    public required string EventType { get; init; }

    public required Guid PlayerId { get; init; }

    public required decimal Amount { get; init; }

    public required decimal Balance { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
