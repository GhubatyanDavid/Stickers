namespace SoundSticker.Infrastructure;

public sealed record PostgresConnectionInfo(
    string? Host,
    int Port,
    string? Database,
    string? Username);
