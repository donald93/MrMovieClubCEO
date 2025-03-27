using MrMovieClubCEO.Models.Database;

namespace MrMovieClubCEO.Interfaces;

public interface IMovieClubRepository
{
    Task InitializeAsync();
    Task<Player?> GetPlayerAsync(string id);
    IReadOnlyCollection<Player> GetPlayers();
    Task UpsertPlayerAsync(Player player);
    Task RegisterLeaderboardChannel(GuildRegistration guildRegistration);
    GuildRegistration GetGuildRegistration();
}