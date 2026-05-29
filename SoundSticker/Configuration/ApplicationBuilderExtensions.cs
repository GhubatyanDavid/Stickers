using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Npgsql;
using SoundSticker.Contracts;
using SoundSticker.Infrastructure;
using SoundSticker.Options;
using SoundSticker.Persistence;

namespace SoundSticker.Configuration;

public static class ApplicationBuilderExtensions
{
    public static async Task ConfigureSoundStickerAsync(
        this WebApplication app,
        PersistenceOptions persistenceOptions)
    {
        app.Logger.LogInformation("SoundSticker API starting. Environment: {EnvironmentName}.", app.Environment.EnvironmentName);
        LogPostgresConfiguration(app, persistenceOptions);

        app.UseForwardedHeaders();
        app.UseHttpsRedirection();
        app.UseCors();
        app.UseRateLimiter();

        var storageRootPath = EnsureStorageDirectories(app);
        await InitializePostgresSchemaAsync(app, persistenceOptions);

        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseExceptionHandler();
        app.UsePostgresExceptionHandler();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storageRootPath),
            RequestPath = StorageOptions.PublicRequestPath
        });
    }

    private static void LogPostgresConfiguration(WebApplication app, PersistenceOptions persistenceOptions)
    {
        if (!persistenceOptions.IsPostgreSql)
        {
            return;
        }

        var connectionInfo = app.Services.GetRequiredService<PostgresConnectionInfo>();
        app.Logger.LogInformation(
            "PostgreSQL config in use. Host: {Host}. Port: {Port}. Database: {Database}. Username: {Username}. ConnectionStringName: {ConnectionStringName}.",
            connectionInfo.Host,
            connectionInfo.Port,
            connectionInfo.Database,
            connectionInfo.Username,
            persistenceOptions.ConnectionStringName);
    }

    private static string EnsureStorageDirectories(WebApplication app)
    {
        var storageOptions = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
        var storageRootPath = storageOptions.GetResolvedRootPath(app.Environment.ContentRootPath);

        Directory.CreateDirectory(storageRootPath);
        Directory.CreateDirectory(Path.Combine(storageRootPath, storageOptions.OriginalsPath));
        Directory.CreateDirectory(Path.Combine(storageRootPath, storageOptions.StickersPath));
        Directory.CreateDirectory(Path.Combine(storageRootPath, storageOptions.PreviewsPath));
        Directory.CreateDirectory(Path.Combine(storageRootPath, storageOptions.TempPath));

        return storageRootPath;
    }

    private static async Task InitializePostgresSchemaAsync(WebApplication app, PersistenceOptions persistenceOptions)
    {
        if (persistenceOptions is not { IsPostgreSql: true, AutoCreateSchema: true })
        {
            return;
        }

        try
        {
            await app.Services.GetRequiredService<PostgreSqlSchemaInitializer>().InitializeAsync();
        }
        catch (NpgsqlException exception)
        {
            app.Logger.LogCritical(
                "PostgreSQL schema initialization failed. SqlState: {SqlState}. Message: {MessageText}",
                PostgresDataSourceFactory.TryGetSqlState(exception),
                exception.Message);
        }
    }

    private static void UsePostgresExceptionHandler(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (NpgsqlException exception)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(
                    "PostgreSQL request failed. Path: {Path}. Method: {Method}. SqlState: {SqlState}. Message: {MessageText}",
                    context.Request.Path,
                    context.Request.Method,
                    PostgresDataSourceFactory.TryGetSqlState(exception),
                    exception.Message);

                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsJsonAsync(new ProblemResponse("Database is unavailable. Check PostgreSQL connection settings."));
                }
            }
        });
    }
}
