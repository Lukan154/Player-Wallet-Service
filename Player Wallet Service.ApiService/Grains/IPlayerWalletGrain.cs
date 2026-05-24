namespace Player_Wallet_Service.ApiService.Grains;

[Alias("PlayerWalletService.Grains.IPlayerWalletGrain")]
public interface IPlayerWalletGrain : IGrainWithGuidKey
{
    [Alias("GetBalanceAsync")]
    Task<decimal> GetBalanceAsync();

    [Alias("AddFundsAsync")]
    Task<decimal> AddFundsAsync(decimal amount);

    [Alias("DeductFundsAsync")]
    Task<decimal> DeductFundsAsync(decimal amount);
}
