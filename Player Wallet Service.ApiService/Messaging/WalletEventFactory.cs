using Player_Wallet_Service.ApiService.Events;
using Player_Wallet_Service.ApiService.Messaging;

namespace Player_Wallet_Service.ApiService.Messaging;

public static class WalletEventFactory
{
    public static WalletEvent FundsAdded(Guid playerId, decimal amount, decimal balance) =>
        new()
        {
            EventType = WalletEventTypes.FundsAdded,
            PlayerId = playerId,
            Amount = amount,
            Balance = balance
        };

    public static WalletEvent FundsDeducted(Guid playerId, decimal amount, decimal balance) =>
        new()
        {
            EventType = WalletEventTypes.FundsDeducted,
            PlayerId = playerId,
            Amount = amount,
            Balance = balance
        };

    public static WalletEvent DeductionRejected(Guid playerId, decimal requestedAmount, decimal balance) =>
        new()
        {
            EventType = WalletEventTypes.DeductionRejected,
            PlayerId = playerId,
            Amount = requestedAmount,
            Balance = balance
        };
}
