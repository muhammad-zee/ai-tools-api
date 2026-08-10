using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

public class WeatherPlugin
{
    private static readonly HttpClient _httpClient = new();
    private readonly string _apiKey;

    public WeatherPlugin(string apiKey)
    {
        _apiKey = apiKey;
    }

    [KernelFunction]
    [Description("Fetches the current weather for a specified city.")]
    public async Task<string> GetCurrentWeatherAsync(
        [Description("The name of the city, e.g., 'London' or 'New York'")] string city, 
        [Description("Unit system: 'metric' (Celsius), 'imperial' (Fahrenheit), or 'standard' (Kelvin)")] string units = "metric")
    {
        string url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={_apiKey}&units={units}";

        try
        {
            var weatherResponse = await _httpClient.GetFromJsonAsync<WeatherResponse>(url);

            if (weatherResponse == null || weatherResponse.Conditions.Count == 0)
            {
                return $"WEATHER_FETCH_FAILED: Could not retrieve weather data for '{city}'.";
            }

            // Fixed property accesses: Conditions instead of Weather, Temperature instead of Temp
            return $"Current weather in {weatherResponse.CityName}: {weatherResponse.Conditions[0].Description}, " +
                   $"Temperature: {weatherResponse.Main.Temperature}°C, " +
                   $"Humidity: {weatherResponse.Main.Humidity}%, " +
                   $"Wind Speed: {weatherResponse.Wind.Speed} m/s.";
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[HTTP Error]: Could not fetch weather - {ex.Message}");
            return $"ERROR: Unable to communicate with the weather service ({ex.Message}).";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error]: {ex.Message}");
            return $"ERROR: An unexpected error occurred while fetching weather for '{city}'.";
        }
    }

    // --- Data Transfer Objects (DTOs) ---

    public record WeatherResponse(
        [property: JsonPropertyName("name")] string CityName,
        [property: JsonPropertyName("main")] MainData Main,
        [property: JsonPropertyName("weather")] List<WeatherCondition> Conditions,
        [property: JsonPropertyName("wind")] WindData Wind
    );

    public record MainData(
        [property: JsonPropertyName("temp")] double Temperature,
        [property: JsonPropertyName("feels_like")] double FeelsLike,
        [property: JsonPropertyName("humidity")] int Humidity
    );

    public record WeatherCondition(
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("main")] string Main
    );

    public record WindData(
        [property: JsonPropertyName("speed")] double Speed
    );
}