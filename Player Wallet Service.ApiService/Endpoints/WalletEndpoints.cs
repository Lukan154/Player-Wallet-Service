using Microsoft.AspNetCore.Http.HttpResults;
using Player_Wallet_Service.ApiService.Grains;
using Player_Wallet_Service.ApiService.Messaging;
using Player_Wallet_Service.ApiService.Models;

namespace Player_Wallet_Service.ApiService.Endpoints;

public static class WalletEndpoints
{
    // Defines API endpoints for player wallet operations such as retrieving balance, adding funds, and deducting funds.
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/players/{playerId:guid}");

        group.MapGet("/balance", GetBalanceAsync)
            .WithName("GetPlayerBalance");

        group.MapPost("/funds", (Guid playerId, FundsRequest request, IGrainFactory grains, IWalletEventPublisher events) =>
            AddFundsAsync(playerId, request, grains, events))
            .WithName("AddPlayerFunds");

        group.MapPost("/funds/deduct", (Guid playerId, FundsRequest request, IGrainFactory grains, IWalletEventPublisher events) =>
            DeductFundsAsync(playerId, request, grains, events))
            .WithName("DeductPlayerFunds");

        return endpoints;
    }

    // Handler for retrieving a player's wallet balance. It interacts with the Orleans grain to get the balance and returns it in the response.
    private static async Task<IResult> GetBalanceAsync(Guid playerId, IGrainFactory grains)
    {
        var grain = grains.GetGrain<IPlayerWalletGrain>(playerId);
        var balance = await grain.GetBalanceAsync();
        return TypedResults.Ok(new { playerId, balance });
    }

    // Handler for adding funds to a player's wallet. It interacts with the Orleans grain to add funds and publishes an event.
    private static async Task<IResult> AddFundsAsync(
        Guid playerId,
        FundsRequest request,
        IGrainFactory grains,
        IWalletEventPublisher events)
    {
        if (request is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["amount"] = ["Request body is required."]
            });
        }

        if (request.Amount <= 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["amount"] = ["Amount must be greater than zero."]
            });
        }

        try
        {
            var grain = grains.GetGrain<IPlayerWalletGrain>(playerId);
            var balance = await grain.AddFundsAsync(request.Amount);
            await events.PublishAsync(WalletEventFactory.FundsAdded(playerId, request.Amount, balance));
            return TypedResults.Ok(new { playerId, balance });
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to add funds");
        }
    }

    // Handler for deducting funds from a player's wallet. It interacts with the Orleans grain to deduct funds and publishes an event.
    private static async Task<IResult> DeductFundsAsync(
        Guid playerId,
        FundsRequest request,
        IGrainFactory grains,
        IWalletEventPublisher events)
    {
        if (request is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["amount"] = ["Request body is required."]
            });
        }

        if (request.Amount <= 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["amount"] = ["Amount must be greater than zero."]
            });
        }

        var grain = grains.GetGrain<IPlayerWalletGrain>(playerId);

        try
        {
            var balance = await grain.DeductFundsAsync(request.Amount);
            await events.PublishAsync(WalletEventFactory.FundsDeducted(playerId, request.Amount, balance));
            return TypedResults.Ok(new { playerId, balance });
        }
        catch (InsufficientFundsException ex)
        {
            await events.PublishAsync(
                WalletEventFactory.DeductionRejected(playerId, request.Amount, ex.Balance));

            return TypedResults.Conflict(new
            {
                playerId,
                balance = ex.Balance,
                requested = ex.Requested,
                error = "Insufficient funds."
            });
        }
    }
}
