using Orleans.Hosting;
using Player_Wallet_Service.ApiService.Endpoints;
using Player_Wallet_Service.ApiService.Infrastructure;
using Player_Wallet_Service.ApiService.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(); // Registers Aspire defaults such as logging, configuration, etc.
builder.AddNpgsqlDataSource("walletdb"); // Registers PostgreSQL datasource
builder.AddKafkaProducer<string, string>("kafka"); // Registers Kafka producer

// Configure Kafka options and register the wallet event publisher
builder.Services.Configure<WalletKafkaOptions>(
    builder.Configuration.GetSection(WalletKafkaOptions.SectionName));
builder.Services.AddSingleton<IWalletEventPublisher, KafkaWalletEventPublisher>();

// Validates the DB connection
var walletConnectionString = builder.Configuration.GetConnectionString("walletdb");
if (string.IsNullOrWhiteSpace(walletConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'walletdb' is missing. Start the app via AppHost (dotnet run on Player Wallet Service.AppHost).");
}

// Ensure Orleans database schema is applied before starting the silo
Console.WriteLine("Applying Orleans database schema if needed...");
await OrleansDatabaseMigrator.InitializeAsync(walletConnectionString);
Console.WriteLine("Orleans database schema ready.");

// Configure Orleans silo with ADO.NET grain storage using the same PostgreSQL connection string
builder.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering(); // Use localhost clustering for development/testing, replace with production clustering in real deployments
    siloBuilder.AddAdoNetGrainStorage(
        "Default",
        options =>
        {
            options.Invariant = "Npgsql";
            options.ConnectionString = walletConnectionString;
        });
});

// Add API services and middleware
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
