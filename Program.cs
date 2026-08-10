using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Embeddings;
using System.ComponentModel;
using Whisper.net;
using Whisper.net.Ggml;

var builder = WebApplication.CreateBuilder(args);

const string AngularClientPolicy = "AngularClient";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClientPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "ggml-base.bin");

// 1. Setup the Kernel with Local Ollama
var kernelBuilder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0070 // This hides the 'experimental' warning for Ollama
kernelBuilder.AddOllamaChatCompletion(
    modelId: "llama3.2",
    endpoint: new Uri("http://localhost:11434")
).AddOllamaTextEmbeddingGeneration(
    modelId: "nomic-embed-text",
    endpoint: new Uri("http://localhost:11434")
);

// 2. Register your C# Tool
var openWeatherApiKey = builder.Configuration["OpenWeatherApiKey"];
if (string.IsNullOrWhiteSpace(openWeatherApiKey))
{
    throw new InvalidOperationException(
        "OpenWeatherApiKey is not configured. Set it in appsettings.Development.json.");
}
kernelBuilder.Plugins.AddFromObject(new WeatherPlugin(openWeatherApiKey));
kernelBuilder.Plugins.AddFromType<WebResearchPlugin>();
kernelBuilder.Plugins.AddFromType<CalculatorPlugin>();
kernelBuilder.Plugins.AddFromType<ReportPlugin>();

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(kernel.GetRequiredService<ITextEmbeddingGenerationService>());
builder.Services.AddSingleton<InMemoryVectorStore>();
builder.Services.AddSingleton<ChatSessionStore>();


if (!File.Exists(modelPath))
{
    using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Base, QuantizationType.NoQuantization);
    using var fileWriter = File.Create(modelPath);
    await modelStream.CopyToAsync(fileWriter);
}

var whisperFactory = WhisperFactory.FromPath(modelPath);
builder.Services.AddSingleton(whisperFactory);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(AngularClientPolicy);

app.MapControllers();

// // 3. Expose the AI agent over HTTP
// app.MapPost("/agent/ask", async (AgentRequest request, Kernel kernel) =>
// {
//     PromptExecutionSettings settings = new()
//     {
//         FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true)
//     };

//     var result = await kernel.InvokePromptAsync(request.Prompt, new KernelArguments(settings));
//     return Results.Ok(new { reply = result.ToString() });
// });

app.Run();

record AgentRequest(string Prompt);
