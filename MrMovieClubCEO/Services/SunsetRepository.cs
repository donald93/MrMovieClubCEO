using System.Text.Json;
using Microsoft.Extensions.Options;
using MrMovieClubCEO.Interfaces;
using MrMovieClubCEO.Models.Configuration;
using MrMovieClubCEO.Models.Contracts;

namespace MrMovieClubCEO.Services;

public class SunsetRepository(IOptions<SunsetApi> sunsetApiOptions, IHttpClientFactory httpClientFactory) : ISunsetRepository
{
    public async Task<DateTime> GetSunsetToday()
    {
        var client = httpClientFactory.CreateClient(sunsetApiOptions.Value.ClientName);
        
        var date = DateTime.Now;
        var dateFormatted = date.ToString("yyyy-MM-dd");
        
        var response = await client.GetAsync($"json?lat=47.6996163&lng=-122.3134438&date={dateFormatted}&formatted=0");

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var sunsetResponse = await response.Content.ReadFromJsonAsync<SunsetResponse>(
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });

                return new DateTimeOffset(sunsetResponse.Results.Sunset, TimeSpan.FromHours(-7)).DateTime;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        
        throw new ApplicationException(response.ReasonPhrase);
        
    }
}