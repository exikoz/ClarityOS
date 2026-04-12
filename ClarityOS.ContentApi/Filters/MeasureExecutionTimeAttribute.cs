using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClarityOS.ContentApi.Filters;

public class MeasureExecutionTimeAttribute : ActionFilterAttribute
{
    private const string StopwatchKey = "MeasureExecutionTime_Stopwatch";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Items[StopwatchKey] = Stopwatch.StartNew();
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.HttpContext.Items[StopwatchKey] is Stopwatch sw)
        {
            sw.Stop();
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<MeasureExecutionTimeAttribute>>();
            logger.LogInformation(
                "Action {Action} executed in {ElapsedMs}ms",
                context.ActionDescriptor.DisplayName,
                sw.ElapsedMilliseconds);
        }
    }
}
