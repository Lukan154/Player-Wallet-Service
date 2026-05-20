namespace Player_Wallet_Service.ApiService.Grains;

public interface IPlayerWalletGrain : IGrainWithGuidKey
{
    Task<decimal> GetBalanceAsync();
}
