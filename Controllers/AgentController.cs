
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
            var agentStepTracker = new AgentStepTracker();
            var requestKernel = _kernel.Clone();
            requestKernel.FunctionInvocationFilters.Add(new AgentStepLoggingFilter(agentStepTracker));

            var history = new ChatHistory();
            history.AddSystemMessage(
                "If none of your available tools are relevant to the user's input, do not attempt to call a tool or write JSON-like text describing a function call. Instead, respond with exactly this: 'I'm not able to help with that. I can research topics, check the weather, or perform calculations — try rephrasing your question around one of those.'"

);

            history.AddSystemMessage(
                "You are a research assistant. Before writing anything, use your available tools to search for " +
                "relevant facts. If one search isn't enough to cover the topic, search again with a more specific " +
                "query. Only write the final article once you have enough information. " +
                "If a tool result starts with SEARCH_FAILED, do not write an answer from your own assumed " +
                "knowledge - tell the user that topic could not be researched instead.");
            history.AddUserMessage(parameter.question);

            PromptExecutionSettings settings = new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true)
            };

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatCompletionService.GetChatMessageContentsAsync(history, settings, requestKernel);

            return Ok(new { reply = response[0].Content ?? string.Empty, steps = agentStepTracker.Steps });
        }





    }


}