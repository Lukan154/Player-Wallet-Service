using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Player_Wallet_Service.Benchmarks;

public static class WalletPlayerPool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<Guid> Create(int size)
    {
        var players = new Guid[size];
        for (var i = 0; i < size; i++)
        {
            players[i] = Guid.NewGuid();
        }

        return players;
    }

    public static Guid PickPlayer(IReadOnlyList<Guid> playerPool, long invocationNumber) =>
        playerPool[(int)(invocationNumber % playerPool.Count)];

    public static async Task SeedBalancesAsync(
        HttpClient httpClient,
        string baseUrl,
        IReadOnlyList<Guid> playerIds,
        decimal initialBalance,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Seeding {playerIds.Count} wallets with balance {initialBalance}...");

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 32,
            CancellationToken = cancellationToken
        };

        var seeded = 0;
        await Parallel.ForEachAsync(playerIds, parallelOptions, async (playerId, ct) =>
        {
            await AddFundsAsync(httpClient, baseUrl, playerId, initialBalance, ct);
            Interlocked.Increment(ref seeded);
        });

        Console.WriteLine($"Seeded {seeded} wallets.");
    }

    private static async Task AddFundsAsync(
        HttpClient httpClient,
        string baseUrl,
        Guid playerId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { amount }, JsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync($"{baseUrl}/players/{playerId:D}/funds", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
