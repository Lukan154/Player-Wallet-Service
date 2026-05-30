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

    // On activation, the grain reads its state from the persistent storage. If the PlayerId is not set (indicating a new grain), it initializes it with the grain's primary key. This ensures that each grain instance is associated with a unique player ID.
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await _state.ReadStateAsync();
        if (_state.State.PlayerId == Guid.Empty)
        {
            _state.State.PlayerId = this.GetPrimaryKey();
        }

        await base.OnActivateAsync(cancellationToken);
    }

    // Retrieves the current balance of the player's wallet. It simply returns the balance stored in the grain's state.
    public Task<decimal> GetBalanceAsync() => Task.FromResult(_state.State.Balance);

    // Adds funds to the player's wallet. It validates that the amount is greater than zero, updates the balance in the grain's state, and writes the updated state back to persistent storage.
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

    // Deducts funds from the player's wallet. It validates that the amount is greater than zero, checks for sufficient funds, updates the balance in the grain's state, and writes the updated state back to persistent storage.
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
