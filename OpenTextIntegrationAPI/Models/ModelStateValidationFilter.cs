using Microsoft.AspNetCore.Mvc.Filters;

public class ModelStateValidationFilter : IActionFilter
{
    private readonly ILogService _logger;

    public ModelStateValidationFilter(ILogService logger)
    {
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        _logger.Log($"ModelStateValidationFilter executing for {context.ActionDescriptor.DisplayName}", LogLevel.DEBUG);

        if (!context.ModelState.IsValid)
        {
            // Log model state errors
            foreach (var state in context.ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    _logger.Log($"Model binding error for {state.Key}: {error.ErrorMessage}", LogLevel.WARNING);
                }
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No action needed after execution
    }
}