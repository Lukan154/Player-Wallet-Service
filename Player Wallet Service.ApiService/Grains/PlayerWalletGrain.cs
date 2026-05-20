using Orleans;
using Orleans.Runtime;

namespace Player_Wallet_Service.ApiService.Grains;

public class PlayerWalletGrain : Grain, IPlayerWalletGrain
{
    private readonly IPersistentState<PlayerWalletGrainState> _state;

    public PlayerWalletGrain(
        [PersistentState("wallet", "Default")] IPersistentState<PlayerWalletGrainState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.PlayerId == Guid.Empty)
        {
            _state.State.PlayerId = this.GetPrimaryKey();
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<decimal> GetBalanceAsync() => Task.FromResult(_state.State.Balance);
}
