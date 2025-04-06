using MrMovieClubCEO;
using MrMovieClubCEO.Interfaces;
using MrMovieClubCEO.Models.Configuration;
using MrMovieClubCEO.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

Console.WriteLine(builder.Configuration.GetDebugView());

var clientSettings = builder.Configuration.GetSection("SunsetApi").Get<SunsetApi>();

builder.Services.AddHttpClient(clientSettings.ClientName, client =>
{
    client.BaseAddress = new Uri(clientSettings.BaseUrl);
});
builder.Services.Configure<SunsetApi>(builder.Configuration.GetSection("SunsetApi"));
builder.Services.Configure<CosmosDbOptions>(builder.Configuration.GetSection("CosmosDb"));
builder.Services.Configure<DiscordOptions>(builder.Configuration.GetSection("Discord"));
builder.Services.AddSingleton<IMovieClubRepository, MovieClubRepository>();
builder.Services.AddSingleton<IDiscordClient, DiscordClient>();
builder.Services.AddSingleton<ISunsetRepository, SunsetRepository>();

var host = builder.Build();
host.Run();