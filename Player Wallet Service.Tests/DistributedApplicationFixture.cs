using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Player_Wallet_Service.Tests;

public sealed class DistributedApplicationFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    public DistributedApplication App { get; private set; } = null!;

    public HttpClient ApiClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        using var cts = new CancellationTokenSource(StartupTimeout);
        var cancellationToken = cts.Token;

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Player_Wallet_Service_AppHost>(cancellationToken);

        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        var app = await appHost.BuildAsync(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        await app.StartAsync(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("kafka", cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice", cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        App = app;
        ApiClient = App.CreateHttpClient("apiservice");
    }

    public async Task DisposeAsync()
    {
        ApiClient.Dispose();
        await App.DisposeAsync();
    }
}
