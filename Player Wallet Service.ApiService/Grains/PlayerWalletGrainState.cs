using Orleans;

namespace Player_Wallet_Service.ApiService.Grains;

[GenerateSerializer]
public class PlayerWalletGrainState
{
    [Id(0)]
    public Guid PlayerId { get; set; }

    [Id(1)]
    public decimal Balance { get; set; }
}
