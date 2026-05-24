var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var walletdb = postgres.AddDatabase("walletdb");

var kafka = builder.AddKafka("kafka");

builder.AddProject<Projects.Player_Wallet_Service_ApiService>("apiservice")
    .WithReference(walletdb)
    .WithReference(kafka)
    .WaitFor(walletdb);

builder.Build().Run();
