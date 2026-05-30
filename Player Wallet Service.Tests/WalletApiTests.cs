using System.Net;
using System.Net.Http.Json;
using Confluent.Kafka;
using Player_Wallet_Service.ApiService.Events;

namespace Player_Wallet_Service.Tests;

[Collection(AspireAppHostCollection.Name)]
public sealed class WalletApiTests(DistributedApplicationFixture fixture)
{
    [Fact]
    public async Task ApiRoot_ReturnsOk()
    {
        using var response = await fixture.ApiClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Tests the API endpoint for retrieving a player's wallet balance. It verifies that a new player starts with a zero balance and that the correct player ID is returned in the response.
    [Fact]
    public async Task GetBalance_NewPlayer_ReturnsZero()
    {
        var playerId = Guid.NewGuid();

        using var response = await WalletTestHttpClient.GetBalanceAsync(fixture.ApiClient, playerId);
        var body = await WalletTestHttpClient.ReadBalanceAsync(response);

        Assert.Equal(playerId, body.PlayerId);
        Assert.Equal(0m, body.Balance);
    }

    // Tests the API endpoint for adding funds to a player's wallet. It verifies that the balance is correctly updated after adding funds and that the correct player ID is returned in the response.
    [Fact]
    public async Task AddFunds_IncreasesBalance()
    {
        var playerId = Guid.NewGuid();

        using var addResponse = await WalletTestHttpClient.AddFundsAsync(fixture.ApiClient, playerId, 100m);
        var addBody = await WalletTestHttpClient.ReadMutationAsync(addResponse);

        Assert.Equal(playerId, addBody.PlayerId);
        Assert.Equal(100m, addBody.Balance);

        using var balanceResponse = await WalletTestHttpClient.GetBalanceAsync(fixture.ApiClient, playerId);
        var balanceBody = await WalletTestHttpClient.ReadBalanceAsync(balanceResponse);

        Assert.Equal(100m, balanceBody.Balance);
    }

    // Tests the API endpoint for deducting funds from a player's wallet. It verifies that the balance is correctly updated after deducting funds and that the correct player ID is returned in the response.
    [Fact]
    public async Task DeductFunds_DecreasesBalance()
    {
        var playerId = Guid.NewGuid();

        await WalletTestHttpClient.AddFundsAsync(fixture.ApiClient, playerId, 100m);

        using var deductResponse = await WalletTestHttpClient.DeductFundsAsync(fixture.ApiClient, playerId, 30m);
        var deductBody = await WalletTestHttpClient.ReadMutationAsync(deductResponse);

        Assert.Equal(70m, deductBody.Balance);

        using var balanceResponse = await WalletTestHttpClient.GetBalanceAsync(fixture.ApiClient, playerId);
        var balanceBody = await WalletTestHttpClient.ReadBalanceAsync(balanceResponse);

        Assert.Equal(70m, balanceBody.Balance);
    }

    // Tests the API endpoint for deducting funds from a player's wallet when there are insufficient funds. It verifies that the operation returns a 409 Conflict status and that the balance remains unchanged.
    [Fact]
    public async Task DeductFunds_InsufficientFunds_Returns409AndLeavesBalanceUnchanged()
    {
        var playerId = Guid.NewGuid();

        await WalletTestHttpClient.AddFundsAsync(fixture.ApiClient, playerId, 50m);

        using var deductResponse = await WalletTestHttpClient.DeductFundsAsync(fixture.ApiClient, playerId, 100m);

        Assert.Equal(HttpStatusCode.Conflict, deductResponse.StatusCode);

        var conflict = await deductResponse.Content.ReadFromJsonAsync<InsufficientFundsResponse>();
        Assert.NotNull(conflict);
        Assert.Equal(playerId, conflict.PlayerId);
        Assert.Equal(50m, conflict.Balance);
        Assert.Equal(100m, conflict.Requested);

        using var balanceResponse = await WalletTestHttpClient.GetBalanceAsync(fixture.ApiClient, playerId);
        var balanceBody = await WalletTestHttpClient.ReadBalanceAsync(balanceResponse);

        Assert.Equal(50m, balanceBody.Balance);
    }

    // Tests the API endpoint for adding funds with an amount of zero. It verifies that the operation returns a 400 Bad Request status.
    [Fact]
    public async Task AddFunds_ZeroAmount_Returns400()
    {
        var playerId = Guid.NewGuid();

        using var response = await WalletTestHttpClient.AddFundsAsync(fixture.ApiClient, playerId, 0m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Tests the API endpoint for adding funds with an invalid JSON payload. It verifies that the operation returns a 400 Bad Request status.
    [Fact]
    public async Task AddFunds_InvalidJson_Returns400()
    {
        var playerId = Guid.NewGuid();

        using var response = await WalletTestHttpClient.PostInvalidJsonAsync(fixture.ApiClient, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Tests a workflow of adding multiple funds, deducting some funds, and then reading the balance to verify that the operations are correctly applied in sequence.
    [Fact]
    public async Task WalletWorkflow_AddMultipleDeductAndReadBalance()
    {
        var playerId = Guid.NewGuid();

        await WalletTestHttpClient.AddFundsAsync(fixture.ApiClient, playerId, 100m);
        await WalletTestHttpClient.AddFundsAsync(fixture.ApiClient, playerId, 50m);
        await WalletTestHttpClient.DeductFundsAsync(fixture.ApiClient, playerId, 25m);

        using var balanceResponse = await WalletTestHttpClient.GetBalanceAsync(fixture.ApiClient, playerId);
        var balanceBody = await WalletTestHttpClient.ReadBalanceAsync(balanceResponse);

        Assert.Equal(125m, balanceBody.Balance);
    }

    // Tests that when funds are added to a player's wallet, a corresponding FundsAdded event is published to the Kafka topic. It uses a Kafka consumer to subscribe to the topic and verifies that the expected event is received with the correct player ID.
    [Fact]
    public async Task AddFunds_PublishesFundsAddedEventToKafka()
    {
        var bootstrapServers = await fixture.App.GetConnectionStringAsync("kafka");
        Assert.False(string.IsNullOrWhiteSpace(bootstrapServers));

        var playerId = Guid.NewGuid();
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = $"wallet-tests-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true,
            SessionTimeoutMs = 60000
        }).Build();

        try
        {
            consumer.Subscribe("wallet-events");

            var assignmentDeadline = DateTime.UtcNow.AddSeconds(60);
            while (consumer.Assignment.Count == 0 && DateTime.UtcNow < assignmentDeadline)
            {
                consumer.Consume(TimeSpan.FromMilliseconds(500));
            }

            Assert.NotEmpty(consumer.Assignment);

            using var addResponse = await WalletTestHttpClient.AddFundsAsync(fixture.ApiClient, playerId, 42m);
            await WalletTestHttpClient.ReadMutationAsync(addResponse);

            var consumeDeadline = DateTime.UtcNow.AddSeconds(60);
            ConsumeResult<string, string>? consumeResult = null;
            while (consumeResult is null && DateTime.UtcNow < consumeDeadline)
            {
                consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(500));
            }

            Assert.NotNull(consumeResult?.Message?.Value);
            Assert.Contains(WalletEventTypes.FundsAdded, consumeResult.Message.Value, StringComparison.Ordinal);
            Assert.Contains(playerId.ToString("D"), consumeResult.Message.Value, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            consumer.Close();
        }
    }
}
