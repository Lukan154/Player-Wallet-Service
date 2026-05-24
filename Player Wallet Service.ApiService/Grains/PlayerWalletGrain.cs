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

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await _state.ReadStateAsync();
        if (_state.State.PlayerId == Guid.Empty)
        {
            _state.State.PlayerId = this.GetPrimaryKey();
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public Task<decimal> GetBalanceAsync() => Task.FromResult(_state.State.Balance);

    public async Task<decimal> AddFundsAsync(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        _state.State.Balance += amount;
        await _state.WriteStateAsync();
        return _state.State.Balance;
    }

    public async Task<decimal> DeductFundsAsync(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (_state.State.Balance < amount)
        {
            throw new InsufficientFundsException(_state.State.Balance, amount);
        }

        _state.State.Balance -= amount;
        await _state.WriteStateAsync();
        return _state.State.Balance;
    }
}
