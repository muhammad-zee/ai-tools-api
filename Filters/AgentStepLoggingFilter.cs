using Microsoft.SemanticKernel;

public class AgentStepLoggingFilter : IFunctionInvocationFilter
{
    private readonly AgentStepTracker _tracker;

    public AgentStepLoggingFilter(AgentStepTracker tracker)
    {
        _tracker = tracker;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var argSummary = context.Arguments.ContainsKey("query")
            ? context.Arguments["query"]?.ToString()
            : null;

        _tracker.Steps.Add(argSummary != null
            ? $"Calling {context.Function.Name}: {argSummary}"
            : $"Calling {context.Function.Name}");

        await next(context);

        var resultText = context.Result?.GetValue<string>() ?? string.Empty;
        var preview = resultText.Length > 100 ? resultText[..100] + "..." : resultText;

        _tracker.Steps.Add(preview);
    }
}
