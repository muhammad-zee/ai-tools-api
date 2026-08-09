
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
    public class AgentController : ControllerBase
    {

        public readonly Kernel _kernel;

        public AgentController(Kernel kernel)
        {
            _kernel = kernel;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] RegChatPayload parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter.question))
            {
                return BadRequest("Question cannot be empty.");
            }

            var history = new ChatHistory();
            history.AddSystemMessage(
                "You are a research assistant. Before writing anything, use your available tools to search for " +
                "relevant facts. If one search isn't enough to cover the topic, search again with a more specific " +
                "query. Only write the final article once you have enough information.");
            history.AddUserMessage(parameter.question);

            PromptExecutionSettings settings = new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true)
            };

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatCompletionService.GetChatMessageContentsAsync(history, settings, _kernel);

            return Ok(new { reply = response[0].Content ?? string.Empty });
        }


      

  
    }


}