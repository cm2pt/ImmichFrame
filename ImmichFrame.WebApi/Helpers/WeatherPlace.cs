using System.Globalization;

namespace ImmichFrame.WebApi.Helpers
{
    public class WeatherPlaceRequestDto
    {
        public string? ApiKey { get; set; }

        /// <summary>"latitude,longitude", as in the WeatherLatLong setting.</summary>
        public string? LatLong { get; set; }

        public string? Language { get; set; }
    }

    public class WeatherPlaceDto
    {
        /// <summary>The place OpenWeatherMap reports for these coordinates (what the frame shows); null when unknown.</summary>
        public string? Name { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Names the weather location the way the frame will, so the admin page can show "Lisbon"
    /// rather than bare coordinates. Uses OpenWeatherMap's current weather for the point.
    /// </summary>
    public static class WeatherPlace
    {
        /// <summary>Reads "lat,long" with a dot as the decimal separator, whatever the server's culture.</summary>
        public static bool TryParseLatLong(string? value, out double latitude, out double longitude)
        {
            latitude = longitude = 0;
            var parts = (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
            return parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
                && latitude is >= -90 and <= 90
                && longitude is >= -180 and <= 180;
        }

        public static async Task<WeatherPlaceDto> Lookup(WeatherPlaceRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return new WeatherPlaceDto { Message = "No OpenWeatherMap API key." };
            }

            if (!TryParseLatLong(request.LatLong, out var latitude, out var longitude))
            {
                return new WeatherPlaceDto { Message = "Enter latitude and longitude, e.g. 38.72, -9.14." };
            }

            try
            {
                var service = new OpenWeatherMap.OpenWeatherMapService(new OpenWeatherMap.OpenWeatherMapOptions
                {
                    ApiKey = request.ApiKey,
                    Language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language
                });
                var weather = await service.GetCurrentWeatherAsync(latitude, longitude);
                return string.IsNullOrWhiteSpace(weather?.CityName)
                    ? new WeatherPlaceDto { Message = "OpenWeatherMap has no place name here." }
                    : new WeatherPlaceDto { Name = weather.CityName };
            }
            catch (Exception ex)
            {
                return new WeatherPlaceDto { Message = $"OpenWeatherMap didn't answer: {ex.Message}" };
            }
        }
    }
}
