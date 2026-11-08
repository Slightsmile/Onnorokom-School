using Npgsql;

namespace Backend;

public class Db
{
    private readonly string _connectionString;

    public Db(IConfiguration config)
    {
        _connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("No connection string configured");
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task EnsureSchemaAsync()
    {
        using var conn = await OpenAsync();
        var path = Environment.GetEnvironmentVariable("INIT_SQL_PATH") ?? "/app/init.sql";
        var sql = await File.ReadAllTextAsync(path);
        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
