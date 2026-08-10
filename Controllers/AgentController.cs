
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
                "You are a research assistant with access to tools. Always try calling a relevant tool first. " +
                "If one search isn't enough to cover the topic, search again with a more specific query. " +
                "\n\n" +
                "PRIORITY RULES, in order:\n" +
                "1. If a tool call succeeds and returns usable data, you MUST use that data to write a normal, " +
                "direct answer. Never use the fallback refusal below in this case.\n" +
                "2. If a tool result starts with SEARCH_FAILED, do not answer from your own assumed knowledge - " +
                "tell the user that specific topic could not be researched.\n" +
                "3. Only if none of your available tools are relevant to the input, and no tool was called at " +
                "all, respond with exactly this and nothing else: 'I'm not able to help with that. I can " +
                "research topics, check the weather, or perform calculations — try rephrasing your question " +
                "around one of those.' Do not write JSON-like text describing a function call in this case.\n" +
                "4. If your task is to research and write a substantial article: once you have drafted the full " +
                "article text, you MUST call SaveReport with a short title and the complete article as the " +
                "content - this is a required step, not optional, and must happen before your final reply. " +
                "Only after SaveReport has been called, give your final reply to the user as a brief summary " +
                "(2-3 sentences) confirming the report was saved - do not repeat the full article text again " +
                "in your final reply.");
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