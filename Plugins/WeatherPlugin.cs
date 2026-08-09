using System.ComponentModel;
using Microsoft.SemanticKernel;

public class WeatherPlugin
{
    [KernelFunction]
    [Description("Use this tool to find out the current temperature and weather conditions for any city like Gojra or Faisalabad.")]
    public string GetWeather(string city)
    {
        // For testing, we return a cold temperature
        Console.WriteLine($"\n[System: Calling your C# code for {city}...]");
        return "The temperature is 12°C and it is quite windy.";
    }
    [KernelFunction]
    [Description("if some one ask about you or ask you about your health")]
    public string GetPersonalInfo(string city)
    {
        // For testing, we return a cold temperature
        Console.WriteLine($"\n[System: Calling your C# code for {city}...]");
        return "I am good and I am an AI Assistant.";
    }
}
