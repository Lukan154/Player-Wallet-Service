namespace Player_Wallet_Service.ApiService.Grains;

public sealed class InsufficientFundsException : Exception
{
    // Custom exception to indicate that a wallet operation failed due to insufficient funds.
    public InsufficientFundsException(decimal balance, decimal requested)
        : base($"Insufficient funds. Balance: {balance}, requested: {requested}.")
    {
        Balance = balance;
        Requested = requested;
    }

    public decimal Balance { get; }
    public decimal Requested { get; }
}
