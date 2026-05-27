namespace SoundSticker.Options;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Provider { get; init; } = "InMemory";

    public string ConnectionStringName { get; init; } = "Postgres";

    public bool AutoCreateSchema { get; init; } = true;

    public bool IsPostgreSql =>
        Provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) ||
        Provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase);
}
