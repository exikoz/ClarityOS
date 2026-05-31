using System.Text.Json;
using ClarityOS.ContentApi.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ClarityOS.ContentApi.Middleware;

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
                ValidationException   => (StatusCodes.Status400BadRequest, "Validation Error", ex.Message),
                NotFoundException     => (StatusCodes.Status404NotFound, "Not Found", ex.Message),
                AiParsingException    => (StatusCodes.Status422UnprocessableEntity, "AI Parsing Error", ex.Message),
                TaskCanceledException => (StatusCodes.Status504GatewayTimeout, "Gateway Timeout", "The AI service did not respond in time."),
                TimeoutException      => (StatusCodes.Status504GatewayTimeout, "Gateway Timeout", "The AI service did not respond in time."),
                HttpRequestException httpEx => MapHttpRequestException(httpEx),
                _                     => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
            };

            var problem = new ProblemDetails
            {
                Status   = statusCode,
                Title    = title,
                Detail   = detail,
                Instance = context.Request.Path,
                Type     = "https://tools.ietf.org/html/rfc7807"
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }

    private static (int statusCode, string title, string detail) MapHttpRequestException(HttpRequestException ex)
    {
        if (ex.StatusCode.HasValue)
        {
            var code = (int)ex.StatusCode.Value;
            return code switch
            {
                429 => (StatusCodes.Status429TooManyRequests, "Rate Limit Exceeded", "The AI service is rate limiting requests. Try again later."),
                401 or 403 => (StatusCodes.Status502BadGateway, "Upstream Authentication Failed", "The AI proxy rejected the request."),
                >= 500 => (StatusCodes.Status502BadGateway, "LLM Service Error", "The AI service encountered an internal error."),
                _ => (StatusCodes.Status502BadGateway, "LLM Service Error", "Failed to communicate with the AI service.")
            };
        }

        return (StatusCodes.Status502BadGateway, "LLM Service Error", "Failed to communicate with the AI service.");
    }
}
