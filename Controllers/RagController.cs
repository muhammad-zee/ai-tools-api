
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

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

        public RagController(InMemoryVectorStore vectorStore, Kernel kernel, ITextEmbeddingGenerationService embeddingGenerator)
        {
            _vectorStore = vectorStore;
            _kernel = kernel;
            _embeddingGenerator = embeddingGenerator;
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
                SessionId = sessionId,
                Vectors = vector
            });
        }

        [HttpPost("chat/{sessionId}")]
        public async Task<IActionResult> Chat(string sessionId, [FromBody] string userMessage)
        {
            var embeddings = _vectorStore.GetEmbeddingsForSession(sessionId);
            if (embeddings.Count == 0)
            {
                return NotFound("No embeddings found for the given session ID.");
            }

            var userEmbedding = await _embeddingGenerator.GenerateEmbeddingsAsync(new List<string> { userMessage });
            var userVector = userEmbedding[0].ToArray();

            // Find the most similar chunk based on cosine similarity
            var mostSimilarChunk = embeddings.OrderByDescending(e => CosineSimilarity(userVector, e.Embedding)).FirstOrDefault();

            // Use the most similar chunk as context for the AI model
            var prompt = $"Context: {mostSimilarChunk.ChunkText}\nUser: {userMessage}\nAI:";
            var result = await _kernel.InvokePromptAsync(prompt);

            return Ok(new { reply = result.ToString() });
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