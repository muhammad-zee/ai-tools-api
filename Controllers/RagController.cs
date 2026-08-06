
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using WeatherForecastAI.Models;

namespace WeatherForecastAI.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class RagController : ControllerBase
    {
        private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);

        private readonly InMemoryVectorStore _vectorStore;
        public readonly Kernel _kernel;
        private readonly ITextEmbeddingGenerationService _embeddingGenerator;
        private readonly ChatSessionStore _chatSessionStore;

        public RagController(InMemoryVectorStore vectorStore, Kernel kernel, ITextEmbeddingGenerationService embeddingGenerator, ChatSessionStore chatSessionStore)
        {
            _vectorStore = vectorStore;
            _kernel = kernel;
            _embeddingGenerator = embeddingGenerator;
            _chatSessionStore = chatSessionStore;
        }

        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest(IFormFile file, string? sessionId = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }
            if (!file.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && !file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) && !file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && !file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !file.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only .txt, .pdf, .docx, .csv, .json, and .xml files are allowed.");
            }
            if (sessionId == null)
            {
                sessionId = Guid.NewGuid().ToString();
            }
            string fileRawText = "";
            if (file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                using (var stream = file.OpenReadStream())
                {
                    using var reader = new PdfReader(stream);
                    for (int page = 1; page <= reader.NumberOfPages; page++)
                    {
                        fileRawText += PdfTextExtractor.GetTextFromPage(reader, page);
                    }
                }
            }

            var textChunks = ChunkText(fileRawText);
            var vector = await _embeddingGenerator.GenerateEmbeddingsAsync(textChunks);

            _vectorStore.RemoveExpired(SessionTtl);

            for (int i = 0; i < textChunks.Count; i++)
            {
                _vectorStore.AddEmbedding(sessionId, textChunks[i], vector[i].ToArray());
            }

            return Ok(new
            {
                sessionId = sessionId
            });
        }

        [HttpPost("chat/{sessionId}")]
        public async Task<IActionResult> Chat(string sessionId, [FromBody] RegChatPayload parameter)
        {
            var question = parameter.question;

            if (string.IsNullOrWhiteSpace(question))
            {
                return BadRequest(new { error = "Question cannot be empty." });
            }

            _vectorStore.RemoveExpired(SessionTtl);
            _chatSessionStore.RemoveExpired(SessionTtl);

            var embeddings = _vectorStore.GetEmbeddingsForSession(sessionId);
            if (embeddings.Count == 0)
            {
                return BadRequest(new { error = "Session does not exist." });
            }

            var userEmbedding = await _embeddingGenerator.GenerateEmbeddingsAsync(new List<string> { question });
            var userVector = userEmbedding[0].ToArray();

            // Find the most similar chunk based on cosine similarity
              var topChunks = embeddings
            .OrderByDescending(e => CosineSimilarity(userVector, e.Embedding))
            .Take(5)
            .Select(e => e.ChunkText);
            var contextText = string.Join("\n---\n", topChunks);


            var history = _chatSessionStore.GetOrCreate(sessionId);
            var workingHistory = new ChatHistory();
            workingHistory.AddSystemMessage($"Use the following context to answer the question. If the answer isn't in the context, say you don't know.\n\nContext:\n{contextText}");
            foreach (var message in history)
            {
                workingHistory.Add(message);
            }
            workingHistory.AddUserMessage(question);



            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatCompletionService.GetChatMessageContentsAsync(workingHistory, kernel: _kernel);
            var reply = response[0].Content ?? string.Empty;

            // Only the plain Q&A goes into long-term memory, not the context blob.
            history.AddUserMessage(question);
            history.AddAssistantMessage(reply);
            _chatSessionStore.Touch(sessionId);


            // // Use the most similar chunk as context for the AI model
            // var prompt = $"Context: {contextText}\nUser: {question}\nAI:";
            // var result = await _kernel.InvokePromptAsync(prompt);

            return Ok(new { reply = reply });

        }
        

        private static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            double dotProduct = 0;
            double magnitudeA = 0;
            double magnitudeB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0)
            {
                return 0;
            }

            return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }

        private static List<string> ChunkText(string text, int chunkSizeWords = 500, int overlapWords = 100)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<string>();

            int step = chunkSizeWords - overlapWords;
            for (int start = 0; start < words.Length; start += step)
            {
                int length = Math.Min(chunkSizeWords, words.Length - start);
                string chunk = string.Join(' ', words, start, length);
                chunks.Add(chunk);

                if (start + length >= words.Length)
                {
                    break;
                }
            }

            return chunks;
        }

    }


}