using Player_Wallet_Service.Benchmarks;

if (args.Any(a => a is "-h" or "--help" or "/?"))
{
    BenchmarkOptions.PrintUsage();
    return;
}

BenchmarkOptions options;
try
{
    options = BenchmarkOptions.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    BenchmarkOptions.PrintUsage();
    return;
}

Console.WriteLine("Player Wallet Service — NBomber load test");
Console.WriteLine($"  Base URL : {options.BaseUrl}");
Console.WriteLine($"  Rate     : {options.RatePerSecond} req/s per scenario");
Console.WriteLine($"  Duration : {options.Duration} per scenario");
Console.WriteLine($"  Scenarios: {string.Join(", ", options.Scenarios)}");
Console.WriteLine($"  Pool size: {options.PlayerPoolSize} player IDs (reused per request)");
Console.WriteLine($"  Cooldown : {options.ScenarioCooldown} between scenarios");
Console.WriteLine();
Console.WriteLine("Prerequisite: AppHost must be running (dotnet run --project \"Player Wallet Service.AppHost\").");
Console.WriteLine();

try
{
    await WalletBenchmarkRunner.RunAsync(options);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
