using Microsoft.Extensions.Logging;
using Npgsql;

namespace Player_Wallet_Service.ApiService.Infrastructure;

public static class OrleansDatabaseMigrator
{
    public static async Task InitializeAsync(
        string connectionString,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        if (await TableExistsAsync(connection, "orleansstorage", cancellationToken)
            && await TableExistsAsync(connection, "orleansquery", cancellationToken))
        {
            logger?.LogInformation("Orleans storage schema already exists.");
            return;
        }

        var scriptsDirectory = Path.Combine(AppContext.BaseDirectory, "scripts", "orleans");
        logger?.LogInformation("Applying Orleans PostgreSQL scripts from {ScriptsDirectory}", scriptsDirectory);

        await ExecuteScriptFileAsync(connection, Path.Combine(scriptsDirectory, "PostgreSQL-Main.sql"), cancellationToken);
        await ExecuteScriptFileAsync(connection, Path.Combine(scriptsDirectory, "PostgreSQL-Persistence.sql"), cancellationToken);

        logger?.LogInformation("Orleans storage schema created.");
    }

    public static Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("OrleansDatabaseMigrator");
        var dataSource = services.GetRequiredService<NpgsqlDataSource>();
        return InitializeAsync(dataSource.ConnectionString, logger, cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = @tableName
            """;
        command.Parameters.AddWithValue("tableName", tableName);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task ExecuteScriptFileAsync(
        NpgsqlConnection connection,
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Orleans SQL script not found: {path}");
        }

        var sql = await File.ReadAllTextAsync(path, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
