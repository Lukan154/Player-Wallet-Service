using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Player_Wallet_Service.Tests;

internal static class WalletTestHttpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task<HttpResponseMessage> GetBalanceAsync(HttpClient client, Guid playerId) =>
        client.GetAsync($"/players/{playerId}/balance");

    public static Task<HttpResponseMessage> AddFundsAsync(HttpClient client, Guid playerId, decimal amount) =>
        PostFundsAsync(client, playerId, "funds", amount);

    public static Task<HttpResponseMessage> DeductFundsAsync(HttpClient client, Guid playerId, decimal amount) =>
        PostFundsAsync(client, playerId, "funds/deduct", amount);

    public static Task<HttpResponseMessage> PostInvalidJsonAsync(HttpClient client, Guid playerId) =>
        client.PostAsync(
            $"/players/{playerId}/funds",
            new StringContent("{amount:100}", Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> PostFundsAsync(
        HttpClient client,
        Guid playerId,
        string route,
        decimal amount)
    {
        var payload = JsonSerializer.Serialize(new { amount }, JsonOptions);
        return client.PostAsync(
            $"/players/{playerId}/{route}",
            new StringContent(payload, Encoding.UTF8, "application/json"));
    }

    public static async Task<WalletBalanceResponse> ReadBalanceAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalletBalanceResponse>(JsonOptions))!;
    }

    public static async Task<WalletBalanceResponse> ReadMutationAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalletBalanceResponse>(JsonOptions))!;
    }
}

internal sealed record WalletBalanceResponse(Guid PlayerId, decimal Balance);

internal sealed record InsufficientFundsResponse(
    Guid PlayerId,
    decimal Balance,
    decimal Requested,
    string Error);
