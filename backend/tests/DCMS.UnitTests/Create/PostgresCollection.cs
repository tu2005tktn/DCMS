using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>Groups the tests that use the shared PostgreSQL test schema and drops that schema once they have all finished.</summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresCollection.Cleanup>
{
    public const string Name = "Postgres";

    public sealed class Cleanup : IAsyncLifetime
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public Task DisposeAsync() => CreateTestKit.DropPostgresSchemaAsync();
    }
}
