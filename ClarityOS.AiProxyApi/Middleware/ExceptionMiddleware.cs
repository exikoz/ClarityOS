using System.Text.Json;
using ClarityOS.AiProxyApi.Exceptions;
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

            var (statusCode, title, detail) = ex switch
            {
                RateLimitException rle => (StatusCodes.Status429TooManyRequests, "Rate Limit Exceeded", rle.Message),
                ExternalAuthException  => (StatusCodes.Status502BadGateway, "Upstream Authentication Failed", "The external AI service rejected our credentials."),
                ExternalServiceException ese => (StatusCodes.Status502BadGateway, "LLM Service Error", ese.Message),
                TimeoutException       => (StatusCodes.Status504GatewayTimeout, "Gateway Timeout", "The external AI service did not respond in time."),
                TaskCanceledException  => (StatusCodes.Status504GatewayTimeout, "Gateway Timeout", "The request to the external AI service was cancelled due to timeout."),
                HttpRequestException   => (StatusCodes.Status502BadGateway, "LLM Service Error", "Failed to communicate with the external AI service."),
                InvalidOperationException => (StatusCodes.Status400BadRequest, "Configuration Error", ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
            };

            var problem = new ProblemDetails
            {
                Status   = statusCode,
                Title    = title,
                Detail   = detail,
                Instance = context.Request.Path,
                Type     = "https://tools.ietf.org/html/rfc7807"
            };

            context.Response.StatusCode  = statusCode;
            context.Response.ContentType = "application/problem+json";

            if (ex is RateLimitException { RetryAfter: not null } rl)
                context.Response.Headers.RetryAfter = ((int)rl.RetryAfter.Value.TotalSeconds).ToString();

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
