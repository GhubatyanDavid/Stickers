using SoundSticker.Configuration;
using SoundSticker.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var persistenceOptions = builder.AddSoundStickerServices();

var app = builder.Build();

await app.ConfigureSoundStickerAsync(persistenceOptions);

app.MapHealthEndpoints();

var api = app.MapGroup("/api");
api.MapUploadEndpoints();
api.MapMediaEndpoints();
api.MapStickerEndpoints();

app.Run();
