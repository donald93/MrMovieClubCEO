namespace MrMovieClubCEO.Models.Contracts;

public record SunsetResponse(
    Results Results,
    string Status,
    string Tzid
);

public record Results(
    DateTime Sunrise,
    DateTime Sunset,
    DateTime SolarNoon,
    int DayLength,
    DateTime CivilTwilightBegin,
    DateTime CivilTwilightEnd,
    DateTime NauticalTwilightBegin,
    DateTime NauticalTwilightEnd,
    DateTime AstronomicalTwilightBegin,
    DateTime AstronomicalTwilightEnd
);