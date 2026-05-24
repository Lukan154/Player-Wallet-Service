namespace Player_Wallet_Service.ApiService.Grains;

public class PlayerWalletGrainState
{
    public Guid PlayerId { get; set; }

    public decimal Balance { get; set; }
}
