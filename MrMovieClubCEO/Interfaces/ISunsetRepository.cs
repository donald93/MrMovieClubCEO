namespace MrMovieClubCEO.Interfaces;

public interface ISunsetRepository
{
    Task<DateTime> GetSunsetToday();
}