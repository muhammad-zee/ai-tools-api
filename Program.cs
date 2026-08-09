using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Embeddings;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

const string AngularClientPolicy = "AngularClient";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClientPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 1. Setup the Kernel with Local Ollama
var kernelBuilder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0070 // This hides the 'experimental' warning for Ollama
kernelBuilder.AddOllamaChatCompletion(
    modelId: "llama3.2:1b",
    endpoint: new Uri("http://localhost:11434")
).AddOllamaTextEmbeddingGeneration(
    modelId: "nomic-embed-text",
    endpoint: new Uri("http://localhost:11434")
);

// 2. Register your C# Tool
kernelBuilder.Plugins.AddFromType<WeatherPlugin>();
kernelBuilder.Plugins.AddFromType<WebResearchPlugin>();

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(kernel.GetRequiredService<ITextEmbeddingGenerationService>());
builder.Services.AddSingleton<InMemoryVectorStore>();
builder.Services.AddSingleton<ChatSessionStore>();

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
