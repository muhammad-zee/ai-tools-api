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
        private readonly Kernel _kernel;

        // Inject the Singleton Kernel registered in your Program.cs
        public DataExtractionController(Kernel kernel)
        {
            _kernel = kernel;
        }

        [HttpPost]
        public async Task<IActionResult> ExtractData(IFormFile file)
        {

            if(file == null || file.Length == 0)
            {
                return BadRequest("No File Uploaded");

            }

            var extension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            if(extension != ".pdf" || file.ContentType != "application/pdf")
            {
                return BadRequest("Invalid file type. Only PDF files are allowed.");

            }


            string pdfRawText = "";
            using(var stream = file.OpenReadStream())
            {
                using var reader = new PdfReader(stream);
                for (int page = 1; page <= reader.NumberOfPages; page++)
                {
                    pdfRawText += PdfTextExtractor.GetTextFromPage(reader, page);
                }
            }



            var settings = new Microsoft.SemanticKernel.Connectors.Ollama.OllamaPromptExecutionSettings
            {
                // Low temperature ensures strict, deterministic data extraction mapping
                Temperature = 0.0f,
                ExtensionData = new Dictionary<string, object>
                {
                    { "format", "json" }
                }
            };


            var arguments = new KernelArguments(settings)
            {
                { "system_prompt", "You are a minified JSON-only generation engine. Never output conversational pleasantries, notes, explanations, text wrappers, or markdown code blocks like ```json. Your output must start with '{' and end with '}'." }
            };
            string executionPrompt = $@"
            System instructions: {arguments["system_prompt"]}

            You are an expert AI data extraction engine. Analyze the document text below.
            Determine whether the document is a 'CV' or an 'Invoice' and return a single valid JSON object.
    
            CRITICAL RULES:
            1. Only populate the object path matching the matched DocumentType. Set the other block to null.
            2. Do not insert formatting marks, code blocks (e.g. ```json), or chat conversational filler.
    
            Target Structure:
            {{
                ""DocumentType"": ""CV"" or ""Invoice"",
                ""Confidence"": {{ ""Score"": 0.95, ""Reason"": ""Explanation"" }},
                ""CvDetails"": {{
                    ""CandidateName"": ""string"", ""ContactEmail"": ""string"", ""ContactPhone"": ""string"",
                    ""Skills"": [""skill1"", ""skill2""],
                    ""Experience"": [{{ ""Company"": ""string"", ""JobTitle"": ""string"", ""Duration"": ""string"", ""KeyResponsibilities"": ""string"" }}],
                    ""Education"": [{{ ""Institution"": ""string"", ""Degree"": ""string"", ""GraduationYear"": ""string"" }}]
                }} or null,
                ""InvoiceDetails"": {{
                    ""InvoiceNumber"": ""string"", ""VendorName"": ""string"", ""IssueDate"": ""string"", ""DueDate"": ""string"", ""TotalAmount"": 0.00, ""Currency"": ""USD"",
                    ""LineItems"": [{{ ""ItemDescription"": ""string"", ""Quantity"": 1, ""UnitPrice"": 0.00, ""LineTotal"": 0.00 }}]
                }} or null
            }}

            Document Text Content:
            {pdfRawText}";
            // Simulate data extraction logic

            

            var result = await _kernel.InvokePromptAsync(executionPrompt);
            string rawModelOutput = result.ToString().Trim();


            // Standard sanitization for stray markdown responses if present
            if (rawModelOutput.StartsWith("```"))
            {
                rawModelOutput = rawModelOutput.Replace("```json", "").Replace("```", "").Trim();
            }

            // Strongly typed serialization validation
            var structuredResponse = JsonSerializer.Deserialize<ExtractedDocumentResult>(rawModelOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var extractedData = new
            {
                response = structuredResponse

            };
            return Ok(extractedData);
        }
    }
}
