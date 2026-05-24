namespace Player_Wallet_Service.ApiService.Events;

public static class WalletEventTypes
{
    public const string FundsAdded = nameof(FundsAdded);
    public const string FundsDeducted = nameof(FundsDeducted);
    public const string DeductionRejected = nameof(DeductionRejected);
}
