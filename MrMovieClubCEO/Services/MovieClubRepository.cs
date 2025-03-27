using System.Collections.Immutable;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using MrMovieClubCEO.Interfaces;
using MrMovieClubCEO.Models.Configuration;
using MrMovieClubCEO.Models.Database;

namespace MrMovieClubCEO.Services;

public class MovieClubRepository(IOptions<CosmosDbOptions> cosmosOptions) : IMovieClubRepository
{
    private static Container _playerContainer;
    private static Container _guildContainer;
    
    public async Task InitializeAsync()
    {
        var client = CreateCosmosClient(cosmosOptions.Value.ConnectionString);
        
        var database = await client.CreateDatabaseIfNotExistsAsync(cosmosOptions.Value.DatabaseName);
        var containerResponse = await database.Database.CreateContainerIfNotExistsAsync(cosmosOptions.Value.ContainerName, "/id");
        _playerContainer = containerResponse.Container;

        var guildContainerResponse =
            await database.Database.CreateContainerIfNotExistsAsync("GuildRegistrations", "/id");
        _guildContainer = guildContainerResponse.Container;
    }

    public async Task<Player?> GetPlayerAsync(string id)
    {
        try
        {
            var response = await _playerContainer.ReadItemAsync<Player>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public IReadOnlyCollection<Player> GetPlayers()
    {
        return _playerContainer.GetItemLinqQueryable<Player>(true)
            .Where(p => p.HasReceivedIntro)
            .ToList();
    }

    public async Task UpsertPlayerAsync(Player player)
    {
        await _playerContainer.UpsertItemAsync(player, new PartitionKey(player.Id));
    }

    public async Task RegisterLeaderboardChannel(GuildRegistration guildRegistration)
    {
        try
        {
            await _guildContainer.UpsertItemAsync(guildRegistration, new PartitionKey(guildRegistration.Id));
        }
        catch (CosmosException ex)
        {
            //TODO: handle error
        }
    }

    public GuildRegistration GetGuildRegistration()
    {
        var guild = _guildContainer.GetItemLinqQueryable<GuildRegistration>(true)
            .FirstOrDefault();

        return guild;
    }
    
    private static CosmosClient CreateCosmosClient(string connectionString)
    {
        CosmosClient client = new(
            connectionString: connectionString,
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                    { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
            });
        return client;
    }
}