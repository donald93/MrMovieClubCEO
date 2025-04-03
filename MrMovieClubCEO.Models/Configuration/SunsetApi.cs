namespace MrMovieClubCEO.Models.Configuration;

public record SunsetApi
{
    public required string ClientName { get; set; }
    public required string BaseUrl { get; set; }
}