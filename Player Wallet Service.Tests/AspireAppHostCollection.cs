namespace Player_Wallet_Service.Tests;

[CollectionDefinition(Name)]
public sealed class AspireAppHostCollection : ICollectionFixture<DistributedApplicationFixture>
{
    public const string Name = "AspireAppHost";
}
