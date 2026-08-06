using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text.Json;
using System.Threading.Tasks;
using static WeatherForecastAI.Models.DataExtractionModel;


namespace WeatherForecastAI.Controllers
{

    [ApiController]
        [Route("[controller]")]
            public class DataExtractionController : ControllerBase
                {
        private const string SystemPrompt = "You are a minified JSON-only generation engine. Never output conversational pleasantries, notes, explanations, text wrappers, or markdown code blocks like ```json. Your output must start with '{' and end with '}'.";

        private readonly Kernel _kernel;

        // Inject the Singleton Kernel registered in your Program.cs
        public DataExtractionController(Kernel kernel)
        {
            _kernel = kernel;
        }

        [HttpPost("invoice")]
        public async Task<IActionResult> ExtractInvoiceData(IFormFile file)
        {
            try
            {
                var (pdfRawText, validationError) = await ValidateAndReadPdfAsync(file);
                if (validationError != null)
                {
                    return validationError;
                }

                string executionPrompt = $@"
            System instructions: {SystemPrompt}

            You are an expert AI data extraction engine. Analyze the invoice document text below and return a single valid JSON object.

            CRITICAL RULES:
            1. Do not insert formatting marks, code blocks (e.g. ```json), or chat conversational filler.

            Target Structure:
            {{
                ""Confidence"": {{ ""Score"": 0.95, ""Reason"": ""Explanation"" }},
                ""InvoiceDetails"": {{
                    ""InvoiceNumber"": ""string"", ""VendorName"": ""string"", ""IssueDate"": ""string"", ""DueDate"": ""string"", ""TotalAmount"": 0.00, ""Currency"": ""USD"",
                    ""LineItems"": [{{ ""ItemDescription"": ""string"", ""Quantity"": 1, ""UnitPrice"": 0.00, ""LineTotal"": 0.00 }}]
                }}
            }}

            Document Text Content:
            {pdfRawText}";

                var structuredResponse = await RunExtractionAsync<InvoiceExtractionResult>(executionPrompt);

                return Ok(new { response = structuredResponse });
            }
            catch (JsonException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    error = "The AI model returned data that could not be parsed as valid JSON.",
                    details = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "An unexpected error occurred while extracting invoice data.",
                    details = ex.Message
                });
            }
        }

        [HttpPost("cv")]
        public async Task<IActionResult> ExtractCvData(IFormFile file)
        {
            try
            {
                var (pdfRawText, validationError) = await ValidateAndReadPdfAsync(file);
                if (validationError != null)
                {
                    return validationError;
                }

                string executionPrompt = $@"
            System instructions: {SystemPrompt}

            You are an expert AI data extraction engine. Analyze the CV / resume document text below and return a single valid JSON object.

            CRITICAL RULES:
            1. Do not insert formatting marks, code blocks (e.g. ```json), or chat conversational filler.

            Target Structure:
            {{
                ""Confidence"": {{ ""Score"": 0.95, ""Reason"": ""Explanation"" }},
                ""CvDetails"": {{
                    ""CandidateName"": ""string"", ""ContactEmail"": ""string"", ""ContactPhone"": ""string"",
                    ""Skills"": [""skill1"", ""skill2""],
                    ""Experience"": [{{ ""Company"": ""string"", ""JobTitle"": ""string"", ""Duration"": ""string"", ""KeyResponsibilities"": ""string"" }}],
                    ""Education"": [{{ ""Institution"": ""string"", ""Degree"": ""string"", ""GraduationYear"": ""string"" }}]
                }}
            }}

            Document Text Content:
            {pdfRawText}";

                var structuredResponse = await RunExtractionAsync<CvExtractionResult>(executionPrompt);

                return Ok(new { response = structuredResponse });
            }
            catch (JsonException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    error = "The AI model returned data that could not be parsed as valid JSON.",
                    details = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "An unexpected error occurred while extracting CV data.",
                    details = ex.Message
                });
            }
        }

        private static async Task<(string pdfRawText, IActionResult validationError)> ValidateAndReadPdfAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return (null, new BadRequestObjectResult("No File Uploaded"));
            }

            var extension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".pdf" || file.ContentType != "application/pdf")
            {
                return (null, new BadRequestObjectResult("Invalid file type. Only PDF files are allowed."));
            }

            string pdfRawText = "";
            using (var stream = file.OpenReadStream())
            {
                using var reader = new PdfReader(stream);
                for (int page = 1; page <= reader.NumberOfPages; page++)
                {
                    pdfRawText += PdfTextExtractor.GetTextFromPage(reader, page);
                }
            }

            return (pdfRawText, null);
        }

        private async Task<T> RunExtractionAsync<T>(string executionPrompt)
        {
            var settings = new Microsoft.SemanticKernel.Connectors.Ollama.OllamaPromptExecutionSettings
            {
                // Low temperature ensures strict, deterministic data extraction mapping
                Temperature = 0.0f,
                NumPredict = 4096,
                ExtensionData = new Dictionary<string, object>
                {
                    { "format", "json" }
                }
            };

            var arguments = new KernelArguments(settings)
            {
                { "system_prompt", SystemPrompt }
            };

            var result = await _kernel.InvokePromptAsync(executionPrompt, arguments);
            string rawModelOutput = result.ToString().Trim();

            // Standard sanitization for stray markdown responses if present
            if (rawModelOutput.StartsWith("```"))
            {
                rawModelOutput = rawModelOutput.Replace("```json", "").Replace("```", "").Trim();
            }

            // Small local models sometimes stop generating mid-token (trailing comma,
            // cut-off string, dangling key). Repair back to the last valid position
            // rather than failing deserialization on otherwise-complete data.
            rawModelOutput = RepairTruncatedJson(rawModelOutput);

            // Strongly typed serialization validation
            return JsonSerializer.Deserialize<T>(rawModelOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        // Feeds the model output through Utf8JsonReader in non-final-block mode, which
        // stops (without throwing) at the last position that formed a complete token
        // instead of requiring the whole payload to be well-formed. Property-name
        // tokens are deliberately excluded from the "safe" watermark so a response
        // truncated right after a dangling key rolls back to before that key, rather
        // than closing the object with an orphaned key and no value.
        private static string RepairTruncatedJson(string json)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes, isFinalBlock: false, state: default);
            var containers = new Stack<char>();
            long safeBytePos = 0;

            try
            {
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                            containers.Push('{');
                            break;
                        case JsonTokenType.StartArray:
                            containers.Push('[');
                            break;
                        case JsonTokenType.EndObject:
                        case JsonTokenType.EndArray:
                            if (containers.Count > 0)
                            {
                                containers.Pop();
                            }
                            break;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        safeBytePos = reader.BytesConsumed;
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed at/after safeBytePos - fall through and repair from there.
            }

            if (containers.Count == 0 && safeBytePos == bytes.Length)
            {
                return json;
            }

            string truncated = System.Text.Encoding.UTF8.GetString(bytes, 0, (int)safeBytePos);
            var repaired = new System.Text.StringBuilder(truncated);
            foreach (char opener in containers)
            {
                repaired.Append(opener == '{' ? '}' : ']');
            }

            return repaired.ToString();
        }
    }
}
