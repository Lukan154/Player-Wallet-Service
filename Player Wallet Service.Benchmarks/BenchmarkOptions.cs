namespace Player_Wallet_Service.Benchmarks;

public sealed class BenchmarkOptions
{
    public string BaseUrl { get; init; } = "http://localhost:5403";

    public int RatePerSecond { get; init; } = 1000;

    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(5);

    public IReadOnlyList<BenchmarkScenario> Scenarios { get; init; } =
        [BenchmarkScenario.GetBalance, BenchmarkScenario.AddFunds, BenchmarkScenario.DeductFunds];

    public int PlayerPoolSize { get; init; } = 5_000;

    public decimal DeductSeedBalance { get; init; } = 1_000_000m;

    public TimeSpan ScenarioCooldown { get; init; } = TimeSpan.FromMinutes(2);

    public static BenchmarkOptions Parse(string[] args)
    {
        var smoke = args.Any(a => a.Equals("--smoke", StringComparison.OrdinalIgnoreCase));
        var noCooldown = args.Any(a => a.Equals("--no-cooldown", StringComparison.OrdinalIgnoreCase));
        var baseUrl = GetArgValue(args, "--base-url")
            ?? Environment.GetEnvironmentVariable("WALLET_BENCHMARK_BASE_URL")
            ?? "http://localhost:5403";

        var rate = int.TryParse(GetArgValue(args, "--rate"), out var parsedRate) ? parsedRate : 1000;
        var durationMinutes = double.TryParse(GetArgValue(args, "--minutes"), out var parsedMinutes) ? parsedMinutes : 5d;
        var scenarioArg = GetArgValue(args, "--scenario") ?? "all";
        var poolSize = int.TryParse(GetArgValue(args, "--pool-size"), out var parsedPoolSize) ? parsedPoolSize : 5_000;
        var cooldownSeconds = double.TryParse(GetArgValue(args, "--cooldown-seconds"), out var parsedCooldown)
            ? parsedCooldown
            : 120d;

        if (smoke)
        {
            rate = 10;
            durationMinutes = 0.15; // ~9 seconds
            cooldownSeconds = 0;
        }

        if (noCooldown)
        {
            cooldownSeconds = 0;
        }

        return new BenchmarkOptions
        {
            BaseUrl = baseUrl.TrimEnd('/'),
            RatePerSecond = rate,
            Duration = TimeSpan.FromMinutes(durationMinutes),
            Scenarios = ParseScenarios(scenarioArg),
            PlayerPoolSize = poolSize,
            ScenarioCooldown = TimeSpan.FromSeconds(cooldownSeconds)
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            Player Wallet Service — NBomber benchmarks

            Usage:
              dotnet run --project "Player Wallet Service.Benchmarks" -- [options]

            Options:
              --base-url <url>     ApiService base URL (default: http://localhost:5403)
              --rate <n>           Target requests per second (default: 1000)
              --minutes <n>        Duration per scenario in minutes (default: 5)
              --scenario <name>    balance | add | deduct | all (default: all)
              --pool-size <n>      Reused player IDs per scenario (default: 5000)
              --cooldown-seconds   Pause between scenarios in seconds (default: 120)
              --no-cooldown        Skip pause between scenarios
              --smoke              Quick run (~10 RPS for ~9 seconds per scenario)

            Environment:
              WALLET_BENCHMARK_BASE_URL   Same as --base-url

            Prerequisites:
              AppHost running (Docker: Postgres, Kafka, ApiService).
              Use the HTTP apiservice URL from the Aspire dashboard if HTTPS/cert fails.
            """);
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static IReadOnlyList<BenchmarkScenario> ParseScenarios(string value) =>
        value.ToLowerInvariant() switch
        {
            "balance" or "get-balance" => [BenchmarkScenario.GetBalance],
            "add" or "add-funds" => [BenchmarkScenario.AddFunds],
            "deduct" or "deduct-funds" => [BenchmarkScenario.DeductFunds],
            "all" => [BenchmarkScenario.GetBalance, BenchmarkScenario.AddFunds, BenchmarkScenario.DeductFunds],
            _ => throw new ArgumentException($"Unknown scenario '{value}'. Use balance, add, deduct, or all.")
        };
}

public enum BenchmarkScenario
{
    GetBalance,
    AddFunds,
    DeductFunds
}
