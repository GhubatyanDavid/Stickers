using Npgsql;

namespace SoundSticker.Persistence;

public sealed class PostgreSqlSchemaInitializer(
    NpgsqlDataSource dataSource,
    ILogger<PostgreSqlSchemaInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            CREATE TABLE IF NOT EXISTS media_files (
                id uuid PRIMARY KEY,
                original_file_name text NOT NULL,
                kind integer NOT NULL,
                content_type text NOT NULL,
                size_bytes bigint NOT NULL,
                relative_path text NOT NULL,
                public_url text NOT NULL,
                preview_exists boolean NOT NULL DEFAULT false,
                preview_duration_ms bigint NULL,
                preview_width integer NULL,
                preview_height integer NULL,
                preview_has_audio boolean NOT NULL DEFAULT false,
                preview_thumbnail_url text NULL,
                owner_user_id text NOT NULL DEFAULT 'legacy',
                created_at timestamp with time zone NOT NULL
            );

            CREATE TABLE IF NOT EXISTS stickers (
                id uuid PRIMARY KEY,
                name text NOT NULL DEFAULT 'Sticker',
                source_media_id uuid NOT NULL REFERENCES media_files(id) ON DELETE RESTRICT,
                cover_image_id uuid NULL REFERENCES media_files(id) ON DELETE SET NULL,
                audio_source_media_id uuid NULL REFERENCES media_files(id) ON DELETE SET NULL,
                audio_mode integer NOT NULL,
                output_format integer NOT NULL DEFAULT 0,
                shape integer NOT NULL DEFAULT 0,
                remove_background boolean NOT NULL DEFAULT false,
                background_color text NULL,
                background_similarity double precision NOT NULL DEFAULT 0.18,
                background_blend double precision NOT NULL DEFAULT 0.08,
                trim_start_ms integer NOT NULL,
                trim_end_ms integer NOT NULL,
                audio_trim_start_ms integer NOT NULL,
                audio_trim_end_ms integer NOT NULL,
                status integer NOT NULL,
                output_relative_path text NULL,
                output_url text NULL,
                error_message text NULL,
                owner_user_id text NOT NULL DEFAULT 'legacy',
                is_public boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL,
                completed_at timestamp with time zone NULL
            );

            CREATE TABLE IF NOT EXISTS sticker_favorites (
                sticker_id uuid NOT NULL REFERENCES stickers(id) ON DELETE CASCADE,
                owner_user_id text NOT NULL,
                created_at timestamp with time zone NOT NULL,
                PRIMARY KEY (sticker_id, owner_user_id)
            );

            CREATE TABLE IF NOT EXISTS telegram_chat_links (
                owner_user_id text PRIMARY KEY,
                chat_id bigint NOT NULL,
                telegram_user_id bigint NULL,
                username text NULL,
                connected_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL
            );

            ALTER TABLE media_files
                ADD COLUMN IF NOT EXISTS owner_user_id text NOT NULL DEFAULT 'legacy';

            ALTER TABLE stickers
                ADD COLUMN IF NOT EXISTS name text NOT NULL DEFAULT 'Sticker';

            ALTER TABLE stickers
                ADD COLUMN IF NOT EXISTS owner_user_id text NOT NULL DEFAULT 'legacy';

            ALTER TABLE stickers
                ADD COLUMN IF NOT EXISTS is_public boolean NOT NULL DEFAULT false;

            ALTER TABLE stickers
                ADD COLUMN IF NOT EXISTS output_format integer NOT NULL DEFAULT 0;

            ALTER TABLE stickers
                ADD COLUMN IF NOT EXISTS shape integer NOT NULL DEFAULT 0;

            ALTER TABLE stickers
                ADD COLUMN IF NOT EXISTS remove_background boolean NOT NULL DEFAULT false;

            ALTER TABLE stickers
                ADD COLUMN IF NOT EXISTS background_color text NULL;

            ALTER TABLE stickers
                ADD COLUMN IF NOT EXISTS background_similarity double precision NOT NULL DEFAULT 0.18;

            ALTER TABLE stickers
                ADD COLUMN IF NOT EXISTS background_blend double precision NOT NULL DEFAULT 0.08;

            CREATE INDEX IF NOT EXISTS ix_media_files_created_at ON media_files (created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_media_files_owner_created_at ON media_files (owner_user_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_stickers_created_at ON stickers (created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_stickers_owner_created_at ON stickers (owner_user_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_stickers_public_ready_created_at ON stickers (is_public, status, created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_stickers_status ON stickers (status);
            CREATE INDEX IF NOT EXISTS ix_sticker_favorites_owner_created_at ON sticker_favorites (owner_user_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_telegram_chat_links_chat_id ON telegram_chat_links (chat_id);
            """);

        await command.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("PostgreSQL persistence schema is ready.");
    }
}
