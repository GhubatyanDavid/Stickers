using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using SoundSticker.FileStorage;
using SoundSticker.Infrastructure;
using SoundSticker.Options;
using SoundSticker.Persistence;
using SoundSticker.Processing;

namespace SoundSticker.Configuration;

public static class ServiceCollectionExtensions
{
    public static PersistenceOptions AddSoundStickerServices(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddProblemDetails();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
        builder.Services.Configure<StickerOptions>(builder.Configuration.GetSection(StickerOptions.SectionName));
        builder.Services.Configure<FfmpegOptions>(builder.Configuration.GetSection(FfmpegOptions.SectionName));
        builder.Services.Configure<PersistenceOptions>(builder.Configuration.GetSection(PersistenceOptions.SectionName));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });

        builder.Services.AddSoundStickerRateLimiting();

        var persistenceOptions = builder.Configuration
            .GetSection(PersistenceOptions.SectionName)
            .Get<PersistenceOptions>() ?? new PersistenceOptions();

        builder.Services.AddPersistence(builder.Configuration, persistenceOptions);
        builder.Services.AddStorageAndProcessing();

        return persistenceOptions;
    }

    private static void AddSoundStickerRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIp(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy("uploads", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIp(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy("sticker-creation", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIp(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
    }

    private static void AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        PersistenceOptions persistenceOptions)
    {
        if (!persistenceOptions.IsPostgreSql)
        {
            services.AddSingleton<IMediaRepository, InMemoryMediaRepository>();
            return;
        }

        var connectionString = configuration.GetConnectionString(persistenceOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"PostgreSQL persistence is enabled, but ConnectionStrings:{persistenceOptions.ConnectionStringName} is missing.");
        }

        services.AddSingleton(_ => PostgresDataSourceFactory.Build(connectionString));
        services.AddSingleton(_ => PostgresDataSourceFactory.GetConnectionInfo(connectionString));
        services.AddSingleton<PostgreSqlSchemaInitializer>();
        services.AddSingleton<IMediaRepository, PostgreSqlMediaRepository>();
    }

    private static void AddStorageAndProcessing(this IServiceCollection services)
    {
        services.AddSingleton<ILocalFileStorage, LocalFileStorage>();
        services.AddSingleton<IStoredFileManager, StoredFileManager>();
        services.AddSingleton<StickerProcessingQueue>();
        services.AddSingleton<StickerProcessingCancellationRegistry>();
        services.AddSingleton<IMediaPreviewAnalyzer, FfmpegMediaPreviewAnalyzer>();
        services.AddSingleton<IStickerProcessor, FfmpegStickerProcessor>();
        services.AddHostedService<StickerProcessingWorker>();
        services.AddHostedService<TempFileCleanupService>();
    }

    private static string GetClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
