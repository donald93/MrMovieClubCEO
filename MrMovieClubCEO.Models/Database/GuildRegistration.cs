namespace MrMovieClubCEO.Models.Database;

public record GuildRegistration
{
    public required string Id { get; set; }
    public required ulong ChannelId { get; set; }
    public required string ChannelName { get; set; }
}