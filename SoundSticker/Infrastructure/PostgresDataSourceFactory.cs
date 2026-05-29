using Npgsql;

namespace SoundSticker.Infrastructure;

public static class PostgresDataSourceFactory
{
    public static NpgsqlDataSource Build(string connectionString)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 5
        };

        return new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString).Build();
    }

    public static PostgresConnectionInfo GetConnectionInfo(string connectionString)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        return new PostgresConnectionInfo(
            connectionStringBuilder.Host,
            connectionStringBuilder.Port,
            connectionStringBuilder.Database,
            connectionStringBuilder.Username);
    }

    public static string? TryGetSqlState(NpgsqlException exception) =>
        exception is PostgresException postgresException ? postgresException.SqlState : null;
}
