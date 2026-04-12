using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ClarityOS.AiProxyApi.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);

            if (context.Response.HasStarted)
            {
                logger.LogWarning("Response has already started, cannot write ProblemDetails.");
                throw;
            }

            var (statusCode, title) = ex switch
            {
                HttpRequestException => (StatusCodes.Status502BadGateway, "LLM Service Error"),
                InvalidOperationException => (StatusCodes.Status400BadRequest, "Configuration Error"),
                _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
            };

            var problem = new ProblemDetails
            {
                Status   = statusCode,
                Title    = title,
                Detail   = ex.Message,
                Instance = context.Request.Path,
                Type     = "https://tools.ietf.org/html/rfc7807"
            };

            context.Response.StatusCode  = statusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
