using Player_Wallet_Service.ApiService.Grains;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKeyedRedisClient("orleans-redis");
builder.AddNpgsqlDataSource("walletdb");
builder.UseOrleans();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "Player Wallet API is running.");

app.MapGet("/players/{playerId:guid}/balance", async (Guid playerId, IGrainFactory grains) =>
{
    var grain = grains.GetGrain<IPlayerWalletGrain>(playerId);
    var balance = await grain.GetBalanceAsync();
    return Results.Ok(new { playerId, balance });
})
.WithName("GetPlayerBalance");

app.MapDefaultEndpoints();

app.Run();
