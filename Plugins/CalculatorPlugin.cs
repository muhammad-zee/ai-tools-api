using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

public class CalculatorPlugin
{
    

    [KernelFunction]
    [Description("Calculates the result of a mathematical expression.")]
    public async Task<string> CalculateExpression([Description("The mathematical expression to evaluate")] string query)
    {

        Console.WriteLine($"\n[System: Calculating the result for '{query}'...]");

        try
        {
            // Use DataTable.Compute to evaluate the expression
            var result = new System.Data.DataTable().Compute(query, null);
            return result.ToString() ?? "Calculation resulted in null.";
        }
        catch (Exception ex)
        {
            return $"CALCULATION_FAILED: Error evaluating expression '{query}': {ex.Message}";
        }
    }
}