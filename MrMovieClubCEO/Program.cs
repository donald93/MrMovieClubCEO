using MrMovieClubCEO;
using MrMovieClubCEO.Interfaces;
using MrMovieClubCEO.Models.Configuration;
using MrMovieClubCEO.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.Configure<CosmosDbOptions>(builder.Configuration.GetSection("CosmosDb"));
builder.Services.Configure<DiscordOptions>(builder.Configuration.GetSection("Discord"));
builder.Services.AddSingleton<IMovieClubRepository, MovieClubRepository>();
builder.Services.AddSingleton<IDiscordClient, DiscordClient>();

var host = builder.Build();
host.Run();