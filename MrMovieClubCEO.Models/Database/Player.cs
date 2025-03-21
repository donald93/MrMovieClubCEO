namespace MrMovieClubCEO;

public record Player
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    public string CurrentPuzzle { get; set; } = string.Empty;
    public bool HasReceivedIntro { get; set; }
}