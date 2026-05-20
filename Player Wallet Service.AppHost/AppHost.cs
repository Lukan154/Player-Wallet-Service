var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.Player_Wallet_Service_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Player_Wallet_Service_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
