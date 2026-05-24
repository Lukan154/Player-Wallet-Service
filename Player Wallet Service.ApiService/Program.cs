using Orleans.Hosting;
using Player_Wallet_Service.ApiService.Endpoints;
using Player_Wallet_Service.ApiService.Infrastructure;
using Player_Wallet_Service.ApiService.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("walletdb");
builder.AddKafkaProducer<string, string>("kafka");

builder.Services.Configure<WalletKafkaOptions>(
    builder.Configuration.GetSection(WalletKafkaOptions.SectionName));
builder.Services.AddSingleton<IWalletEventPublisher, KafkaWalletEventPublisher>();

var walletConnectionString = builder.Configuration.GetConnectionString("walletdb");
if (string.IsNullOrWhiteSpace(walletConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'walletdb' is missing. Start the app via AppHost (dotnet run on Player Wallet Service.AppHost).");
}

Console.WriteLine("Applying Orleans database schema if needed...");
await OrleansDatabaseMigrator.InitializeAsync(walletConnectionString);
Console.WriteLine("Orleans database schema ready.");

builder.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddAdoNetGrainStorage(
        "Default",
        options =>
        {
            options.Invariant = "Npgsql";
            options.ConnectionString = walletConnectionString;
        });
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "Player Wallet API is running.");

app.MapWalletEndpoints();

app.MapDefaultEndpoints();

Console.WriteLine("Starting Player Wallet API...");
app.Run();
