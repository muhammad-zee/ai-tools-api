using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

public class WebResearchPlugin
{
    private static readonly HttpClient _httpClient = new();

    static WebResearchPlugin()
    {
        // Wikipedia's API requires a descriptive User-Agent identifying the calling app.
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherForecastAI-ResearchAgent/1.0");
    }

    [KernelFunction]
    [Description("Searches Wikipedia for factual information on a given topic and returns a summary.")]
    public async Task<string> SearchTopic([Description("The topic to search for")] string query)
    {
        Console.WriteLine($"\n[System: Searching Wikipedia for '{query}'...]");

        var pageTitle = await FindBestMatchingTitleAsync(query);
        if (pageTitle == null)
        {
            return $"No Wikipedia results found for '{query}'.";
        }

        return await GetPageSummaryAsync(pageTitle);
    }

    private static async Task<string?> FindBestMatchingTitleAsync(string query)
    {
        var searchUrl = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&format=json&srlimit=1";

        using var response = await _httpClient.GetAsync(searchUrl);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        var results = document.RootElement.GetProperty("query").GetProperty("search");
        if (results.GetArrayLength() == 0)
        {
            return null;
        }

        return results[0].GetProperty("title").GetString();
    }

    private static async Task<string> GetPageSummaryAsync(string pageTitle)
    {
        var summaryUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(pageTitle)}";

        using var response = await _httpClient.GetAsync(summaryUrl);
        if (!response.IsSuccessStatusCode)
        {
            return $"Could not retrieve a summary for '{pageTitle}'.";
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        return document.RootElement.TryGetProperty("extract", out var extract)
            ? extract.GetString() ?? "No summary available."
            : "No summary available.";
    }
}
