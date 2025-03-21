namespace MrMovieClubCEO;

public record Puzzle
{
    public required string Id { get; set; }
    public required string Question { get; set; }
    public required IReadOnlyCollection<string> Answers { get; set; }
}