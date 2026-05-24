using Player_Wallet_Service.ApiService.Events;

namespace Player_Wallet_Service.ApiService.Messaging;

public interface IWalletEventPublisher
{
    Task PublishAsync(WalletEvent walletEvent, CancellationToken cancellationToken = default);
}
