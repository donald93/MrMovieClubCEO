namespace MrMovieClubCEO.Models.Configuration;

public record CosmosDbOptions
{
    public required string ConnectionString { get; set; }
    public required string DatabaseName { get; set; }
    public required string ContainerName { get; set; }

}