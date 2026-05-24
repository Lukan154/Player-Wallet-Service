using System.Net.Http;
using System.Net.Security;
using System.Text;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;

namespace Player_Wallet_Service.Benchmarks;

public static class WalletBenchmarkRunner
{
    public static async Task RunAsync(BenchmarkOptions options, CancellationToken cancellationToken = default)
    {
        using var warmupClient = CreateHttpClient();
        await EnsureApiIsReachableAsync(warmupClient, options.BaseUrl, cancellationToken);

        var playerPool = WalletPlayerPool.Create(options.PlayerPoolSize);
        Console.WriteLine($"Using player pool of {playerPool.Count} IDs (reused across requests).");

        if (options.Scenarios.Contains(BenchmarkScenario.DeductFunds))
        {
            await WalletPlayerPool.SeedBalancesAsync(
                warmupClient,
                options.BaseUrl,
                playerPool,
                options.DeductSeedBalance,
                cancellationToken);
        }

        using var httpClient = CreateHttpClient();

        for (var i = 0; i < options.Scenarios.Count; i++)
        {
            var scenario = options.Scenarios[i];
            Console.WriteLine();
            Console.WriteLine($"=== {scenario} | {options.RatePerSecond} RPS | {options.Duration} ===");

            var nbomberScenario = scenario switch
            {
                BenchmarkScenario.GetBalance => CreateGetBalanceScenario(httpClient, options, playerPool),
                BenchmarkScenario.AddFunds => CreateAddFundsScenario(httpClient, options, playerPool),
                BenchmarkScenario.DeductFunds => CreateDeductFundsScenario(httpClient, options, playerPool),
                _ => throw new ArgumentOutOfRangeException(nameof(scenario))
            };

            NBomberRunner
                .RegisterScenarios(nbomberScenario)
                .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
                .WithReportFolder($"reports/{scenario.ToString().ToLowerInvariant()}")
                .Run();

            var isLastScenario = i == options.Scenarios.Count - 1;
            if (!isLastScenario && options.ScenarioCooldown > TimeSpan.Zero)
            {
                Console.WriteLine();
                Console.WriteLine($"Cooling down for {options.ScenarioCooldown} before next scenario...");
                await Task.Delay(options.ScenarioCooldown, cancellationToken);
            }
        }
    }

    private static ScenarioProps CreateGetBalanceScenario(
        HttpClient httpClient,
        BenchmarkOptions options,
        IReadOnlyList<Guid> playerPool) =>
        Scenario.Create("get_balance", async context =>
        {
            var playerId = WalletPlayerPool.PickPlayer(playerPool, context.InvocationNumber);
            var request = Http.CreateRequest("GET", $"{options.BaseUrl}/players/{playerId:D}/balance");
            return await Http.Send(httpClient, request);
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(
                rate: options.RatePerSecond,
                interval: TimeSpan.FromSeconds(1),
                during: options.Duration));

    private static ScenarioProps CreateAddFundsScenario(
        HttpClient httpClient,
        BenchmarkOptions options,
        IReadOnlyList<Guid> playerPool) =>
        Scenario.Create("add_funds", async context =>
        {
            var playerId = WalletPlayerPool.PickPlayer(playerPool, context.InvocationNumber);
            var request = Http.CreateRequest("POST", $"{options.BaseUrl}/players/{playerId:D}/funds")
                .WithHeader("Content-Type", "application/json")
                .WithBody(new StringContent("{\"amount\":1}", Encoding.UTF8, "application/json"));
            return await Http.Send(httpClient, request);
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(
                rate: options.RatePerSecond,
                interval: TimeSpan.FromSeconds(1),
                during: options.Duration));

    private static ScenarioProps CreateDeductFundsScenario(
        HttpClient httpClient,
        BenchmarkOptions options,
        IReadOnlyList<Guid> playerPool) =>
        Scenario.Create("deduct_funds", async context =>
        {
            var playerId = WalletPlayerPool.PickPlayer(playerPool, context.InvocationNumber);
            var request = Http.CreateRequest("POST", $"{options.BaseUrl}/players/{playerId:D}/funds/deduct")
                .WithHeader("Content-Type", "application/json")
                .WithBody(new StringContent("{\"amount\":1}", Encoding.UTF8, "application/json"));
            return await Http.Send(httpClient, request);
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(
                rate: options.RatePerSecond,
                interval: TimeSpan.FromSeconds(1),
                during: options.Duration));

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 256,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = static (_, _, _, _) => true
            }
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static async Task EnsureApiIsReachableAsync(
        HttpClient httpClient,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync($"{baseUrl}/", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Wallet API at {baseUrl} returned {(int)response.StatusCode}. Check AppHost logs and try the apiservice URL from the Aspire dashboard.");
            }

            Console.WriteLine($"Wallet API reachable at {baseUrl}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"""
                Wallet API is not reachable at {baseUrl}.

                Connection failed — is AppHost running?

                Terminal 1:
                  dotnet run --project "Player Wallet Service.AppHost"

                Terminal 2 (wait until apiservice is healthy in the Aspire dashboard):
                  dotnet run --project "Player Wallet Service.Benchmarks" -- --smoke

                If the port differs, use the HTTP apiservice URL from the dashboard:
                  dotnet run --project "Player Wallet Service.Benchmarks" -- --smoke --base-url http://localhost:<port>
                """,
                ex);
        }
    }
}
