using Npgsql;
using NpgsqlTypes;
using SoundSticker.Domain;

namespace SoundSticker.Persistence;

public sealed class PostgreSqlMediaRepository(NpgsqlDataSource dataSource) : IMediaRepository
{
    private const string MediaFileColumns = """
        id,
        original_file_name,
        kind,
        content_type,
        size_bytes,
        relative_path,
        public_url,
        preview_exists,
        preview_duration_ms,
        preview_width,
        preview_height,
        preview_has_audio,
        preview_thumbnail_url,
        created_at
        """;

    private const string StickerColumns = """
        id,
        source_media_id,
        cover_image_id,
        audio_source_media_id,
        audio_mode,
        trim_start_ms,
        trim_end_ms,
        audio_trim_start_ms,
        audio_trim_end_ms,
        status,
        output_relative_path,
        output_url,
        error_message,
        created_at,
        completed_at
        """;

    public void AddMediaFile(MediaFile mediaFile)
    {
        using var command = dataSource.CreateCommand("""
            INSERT INTO media_files (
                id,
                original_file_name,
                kind,
                content_type,
                size_bytes,
                relative_path,
                public_url,
                preview_exists,
                preview_duration_ms,
                preview_width,
                preview_height,
                preview_has_audio,
                preview_thumbnail_url,
                created_at
            )
            VALUES (
                @id,
                @original_file_name,
                @kind,
                @content_type,
                @size_bytes,
                @relative_path,
                @public_url,
                @preview_exists,
                @preview_duration_ms,
                @preview_width,
                @preview_height,
                @preview_has_audio,
                @preview_thumbnail_url,
                @created_at
            )
            ON CONFLICT (id) DO UPDATE SET
                original_file_name = EXCLUDED.original_file_name,
                kind = EXCLUDED.kind,
                content_type = EXCLUDED.content_type,
                size_bytes = EXCLUDED.size_bytes,
                relative_path = EXCLUDED.relative_path,
                public_url = EXCLUDED.public_url,
                preview_exists = EXCLUDED.preview_exists,
                preview_duration_ms = EXCLUDED.preview_duration_ms,
                preview_width = EXCLUDED.preview_width,
                preview_height = EXCLUDED.preview_height,
                preview_has_audio = EXCLUDED.preview_has_audio,
                preview_thumbnail_url = EXCLUDED.preview_thumbnail_url,
                created_at = EXCLUDED.created_at;
            """);

        AddMediaFileParameters(command, mediaFile);
        command.ExecuteNonQuery();
    }

    public void UpdateMediaFile(MediaFile mediaFile) => AddMediaFile(mediaFile);

    public MediaFile? GetMediaFile(Guid id)
    {
        using var command = dataSource.CreateCommand($"SELECT {MediaFileColumns} FROM media_files WHERE id = @id;");
        AddGuid(command, "id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadMediaFile(reader) : null;
    }

    public IReadOnlyCollection<MediaFile> GetMediaFiles()
    {
        using var command = dataSource.CreateCommand($"SELECT {MediaFileColumns} FROM media_files ORDER BY created_at DESC;");
        using var reader = command.ExecuteReader();
        var mediaFiles = new List<MediaFile>();

        while (reader.Read())
        {
            mediaFiles.Add(ReadMediaFile(reader));
        }

        return mediaFiles;
    }

    public void AddSticker(Sticker sticker)
    {
        using var command = dataSource.CreateCommand("""
            INSERT INTO stickers (
                id,
                source_media_id,
                cover_image_id,
                audio_source_media_id,
                audio_mode,
                trim_start_ms,
                trim_end_ms,
                audio_trim_start_ms,
                audio_trim_end_ms,
                status,
                output_relative_path,
                output_url,
                error_message,
                created_at,
                completed_at
            )
            VALUES (
                @id,
                @source_media_id,
                @cover_image_id,
                @audio_source_media_id,
                @audio_mode,
                @trim_start_ms,
                @trim_end_ms,
                @audio_trim_start_ms,
                @audio_trim_end_ms,
                @status,
                @output_relative_path,
                @output_url,
                @error_message,
                @created_at,
                @completed_at
            )
            ON CONFLICT (id) DO UPDATE SET
                source_media_id = EXCLUDED.source_media_id,
                cover_image_id = EXCLUDED.cover_image_id,
                audio_source_media_id = EXCLUDED.audio_source_media_id,
                audio_mode = EXCLUDED.audio_mode,
                trim_start_ms = EXCLUDED.trim_start_ms,
                trim_end_ms = EXCLUDED.trim_end_ms,
                audio_trim_start_ms = EXCLUDED.audio_trim_start_ms,
                audio_trim_end_ms = EXCLUDED.audio_trim_end_ms,
                status = EXCLUDED.status,
                output_relative_path = EXCLUDED.output_relative_path,
                output_url = EXCLUDED.output_url,
                error_message = EXCLUDED.error_message,
                created_at = EXCLUDED.created_at,
                completed_at = EXCLUDED.completed_at;
            """);

        AddStickerParameters(command, sticker);
        command.ExecuteNonQuery();
    }

    public void UpdateSticker(Sticker sticker) => AddSticker(sticker);

    public Sticker? GetSticker(Guid id)
    {
        using var command = dataSource.CreateCommand($"SELECT {StickerColumns} FROM stickers WHERE id = @id;");
        AddGuid(command, "id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSticker(reader) : null;
    }

    public IReadOnlyCollection<Sticker> GetStickers()
    {
        using var command = dataSource.CreateCommand($"SELECT {StickerColumns} FROM stickers ORDER BY created_at DESC;");
        using var reader = command.ExecuteReader();
        var stickers = new List<Sticker>();

        while (reader.Read())
        {
            stickers.Add(ReadSticker(reader));
        }

        return stickers;
    }

    public Sticker? RemoveSticker(Guid id)
    {
        var sticker = GetSticker(id);
        if (sticker is null)
        {
            return null;
        }

        using var command = dataSource.CreateCommand("DELETE FROM stickers WHERE id = @id;");
        AddGuid(command, "id", id);
        command.ExecuteNonQuery();
        return sticker;
    }

    private static void AddMediaFileParameters(NpgsqlCommand command, MediaFile mediaFile)
    {
        AddGuid(command, "id", mediaFile.Id);
        AddText(command, "original_file_name", mediaFile.OriginalFileName);
        AddInteger(command, "kind", (int)mediaFile.Kind);
        AddText(command, "content_type", mediaFile.ContentType);
        AddBigint(command, "size_bytes", mediaFile.SizeBytes);
        AddText(command, "relative_path", mediaFile.RelativePath);
        AddText(command, "public_url", mediaFile.PublicUrl);
        AddBoolean(command, "preview_exists", mediaFile.Preview is not null);
        AddNullableBigint(command, "preview_duration_ms", mediaFile.Preview?.DurationMs);
        AddNullableInteger(command, "preview_width", mediaFile.Preview?.Width);
        AddNullableInteger(command, "preview_height", mediaFile.Preview?.Height);
        AddBoolean(command, "preview_has_audio", mediaFile.Preview?.HasAudio ?? false);
        AddNullableText(command, "preview_thumbnail_url", mediaFile.Preview?.ThumbnailUrl);
        AddTimestamp(command, "created_at", mediaFile.CreatedAt);
    }

    private static void AddStickerParameters(NpgsqlCommand command, Sticker sticker)
    {
        AddGuid(command, "id", sticker.Id);
        AddGuid(command, "source_media_id", sticker.SourceMediaId);
        AddNullableGuid(command, "cover_image_id", sticker.CoverImageId);
        AddNullableGuid(command, "audio_source_media_id", sticker.AudioSourceMediaId);
        AddInteger(command, "audio_mode", (int)sticker.AudioMode);
        AddInteger(command, "trim_start_ms", sticker.TrimStartMs);
        AddInteger(command, "trim_end_ms", sticker.TrimEndMs);
        AddInteger(command, "audio_trim_start_ms", sticker.AudioTrimStartMs);
        AddInteger(command, "audio_trim_end_ms", sticker.AudioTrimEndMs);
        AddInteger(command, "status", (int)sticker.Status);
        AddNullableText(command, "output_relative_path", sticker.OutputRelativePath);
        AddNullableText(command, "output_url", sticker.OutputUrl);
        AddNullableText(command, "error_message", sticker.ErrorMessage);
        AddTimestamp(command, "created_at", sticker.CreatedAt);
        AddNullableTimestamp(command, "completed_at", sticker.CompletedAt);
    }

    private static MediaFile ReadMediaFile(NpgsqlDataReader reader)
    {
        MediaPreview? preview = null;
        if (GetBoolean(reader, "preview_exists"))
        {
            preview = new MediaPreview(
                GetNullableInt64(reader, "preview_duration_ms"),
                GetNullableInt32(reader, "preview_width"),
                GetNullableInt32(reader, "preview_height"),
                GetBoolean(reader, "preview_has_audio"),
                GetNullableString(reader, "preview_thumbnail_url"));
        }

        return MediaFile.Restore(
            GetGuid(reader, "id"),
            GetString(reader, "original_file_name"),
            (MediaKind)GetInt32(reader, "kind"),
            GetString(reader, "content_type"),
            GetInt64(reader, "size_bytes"),
            GetString(reader, "relative_path"),
            GetString(reader, "public_url"),
            preview,
            GetDateTimeOffset(reader, "created_at"));
    }

    private static Sticker ReadSticker(NpgsqlDataReader reader) =>
        Sticker.Restore(
            GetGuid(reader, "id"),
            GetGuid(reader, "source_media_id"),
            GetNullableGuid(reader, "cover_image_id"),
            GetNullableGuid(reader, "audio_source_media_id"),
            (StickerAudioMode)GetInt32(reader, "audio_mode"),
            GetInt32(reader, "trim_start_ms"),
            GetInt32(reader, "trim_end_ms"),
            GetInt32(reader, "audio_trim_start_ms"),
            GetInt32(reader, "audio_trim_end_ms"),
            (StickerStatus)GetInt32(reader, "status"),
            GetNullableString(reader, "output_relative_path"),
            GetNullableString(reader, "output_url"),
            GetNullableString(reader, "error_message"),
            GetDateTimeOffset(reader, "created_at"),
            GetNullableDateTimeOffset(reader, "completed_at"));

    private static void AddGuid(NpgsqlCommand command, string name, Guid value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Uuid);
        parameter.Value = value;
    }

    private static void AddNullableGuid(NpgsqlCommand command, string name, Guid? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Uuid);
        parameter.Value = value ?? (object)DBNull.Value;
    }

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

    private static void AddInteger(NpgsqlCommand command, string name, int value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Integer);
        parameter.Value = value;
    }

    private static void AddNullableInteger(NpgsqlCommand command, string name, int? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Integer);
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

    private static void AddBoolean(NpgsqlCommand command, string name, bool value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Boolean);
        parameter.Value = value;
    }

    private static void AddTimestamp(NpgsqlCommand command, string name, DateTimeOffset value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.TimestampTz);
        parameter.Value = value.UtcDateTime;
    }

    private static void AddNullableTimestamp(NpgsqlCommand command, string name, DateTimeOffset? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.TimestampTz);
        parameter.Value = value?.UtcDateTime ?? (object)DBNull.Value;
    }

    private static Guid GetGuid(NpgsqlDataReader reader, string name) =>
        reader.GetGuid(reader.GetOrdinal(name));

    private static Guid? GetNullableGuid(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static string GetString(NpgsqlDataReader reader, string name) =>
        reader.GetString(reader.GetOrdinal(name));

    private static string? GetNullableString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int GetInt32(NpgsqlDataReader reader, string name) =>
        reader.GetInt32(reader.GetOrdinal(name));

    private static int? GetNullableInt32(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static long GetInt64(NpgsqlDataReader reader, string name) =>
        reader.GetInt64(reader.GetOrdinal(name));

    private static long? GetNullableInt64(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static bool GetBoolean(NpgsqlDataReader reader, string name) =>
        reader.GetBoolean(reader.GetOrdinal(name));

    private static DateTimeOffset GetDateTimeOffset(NpgsqlDataReader reader, string name)
    {
        var value = reader.GetDateTime(reader.GetOrdinal(name));
        return ToDateTimeOffset(value);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return ToDateTimeOffset(reader.GetDateTime(ordinal));
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return new DateTimeOffset(utcValue);
    }
}
