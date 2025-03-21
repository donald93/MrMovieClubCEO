using MrMovieClubCEO;
using MrMovieClubCEO.Models.Configuration;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.Configure<CosmosDbOptions>(builder.Configuration.GetSection("CosmosDb"));
builder.Services.Configure<DiscordOptions>(builder.Configuration.GetSection("Discord"));

var host = builder.Build();
host.Run();