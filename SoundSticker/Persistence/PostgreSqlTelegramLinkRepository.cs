using Npgsql;
using NpgsqlTypes;
using SoundSticker.Domain;

namespace SoundSticker.Persistence;

public sealed class PostgreSqlTelegramLinkRepository(NpgsqlDataSource dataSource) : ITelegramLinkRepository
{
    public void UpsertLink(TelegramChatLink link)
    {
        using var command = dataSource.CreateCommand("""
            INSERT INTO telegram_chat_links (
                owner_user_id,
                chat_id,
                telegram_user_id,
                username,
                connected_at,
                updated_at
            )
            VALUES (
                @owner_user_id,
                @chat_id,
                @telegram_user_id,
                @username,
                @connected_at,
                @updated_at
            )
            ON CONFLICT (owner_user_id) DO UPDATE SET
                chat_id = EXCLUDED.chat_id,
                telegram_user_id = EXCLUDED.telegram_user_id,
                username = EXCLUDED.username,
                updated_at = EXCLUDED.updated_at;
            """);

        AddText(command, "owner_user_id", link.OwnerUserId);
        AddBigint(command, "chat_id", link.ChatId);
        AddNullableBigint(command, "telegram_user_id", link.TelegramUserId);
        AddNullableText(command, "username", link.Username);
        AddTimestamp(command, "connected_at", link.ConnectedAt);
        AddTimestamp(command, "updated_at", link.UpdatedAt);
        command.ExecuteNonQuery();
    }

    public TelegramChatLink? GetLinkByOwner(string ownerUserId)
    {
        using var command = dataSource.CreateCommand("""
            SELECT owner_user_id, chat_id, telegram_user_id, username, connected_at, updated_at
            FROM telegram_chat_links
            WHERE owner_user_id = @owner_user_id;
            """);

        AddText(command, "owner_user_id", ownerUserId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadLink(reader) : null;
    }

    private static TelegramChatLink ReadLink(NpgsqlDataReader reader) =>
        new(
            GetString(reader, "owner_user_id"),
            GetInt64(reader, "chat_id"),
            GetNullableInt64(reader, "telegram_user_id"),
            GetNullableString(reader, "username"),
            GetDateTimeOffset(reader, "connected_at"),
            GetDateTimeOffset(reader, "updated_at"));

    private static void AddText(NpgsqlCommand command, string name, string value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Text);
        parameter.Value = value;
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Text);
        parameter.Value = value ?? (object)DBNull.Value;
    }

    private static void AddBigint(NpgsqlCommand command, string name, long value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Bigint);
        parameter.Value = value;
    }

    private static void AddNullableBigint(NpgsqlCommand command, string name, long? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Bigint);
        parameter.Value = value ?? (object)DBNull.Value;
    }

    private static void AddTimestamp(NpgsqlCommand command, string name, DateTimeOffset value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.TimestampTz);
        parameter.Value = value.UtcDateTime;
    }

    private static string GetString(NpgsqlDataReader reader, string name) =>
        reader.GetString(reader.GetOrdinal(name));

    private static string? GetNullableString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static long GetInt64(NpgsqlDataReader reader, string name) =>
        reader.GetInt64(reader.GetOrdinal(name));

    private static long? GetNullableInt64(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static DateTimeOffset GetDateTimeOffset(NpgsqlDataReader reader, string name)
    {
        var value = reader.GetDateTime(reader.GetOrdinal(name));
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return new DateTimeOffset(utcValue);
    }
}
