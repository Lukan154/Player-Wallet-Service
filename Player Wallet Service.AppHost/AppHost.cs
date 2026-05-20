var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .AddDatabase("walletdb");

var kafka = builder.AddKafka("kafka");

var redis = builder.AddRedis("orleans-redis");

var orleans = builder.AddOrleans("orleans")
    .WithClustering(redis)
    .WithGrainStorage("Default", redis);

var apiService = builder.AddProject<Projects.Player_Wallet_Service_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(orleans)
    .WithReference(postgres)
    .WithReference(kafka)
    .WaitFor(postgres)
    .WaitFor(kafka)
    .WaitFor(redis);

builder.Build().Run();
