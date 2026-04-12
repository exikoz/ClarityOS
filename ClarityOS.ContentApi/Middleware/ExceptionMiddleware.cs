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

            var (statusCode, title) = ex switch
            {
                ValidationException   => (StatusCodes.Status400BadRequest, "Validation Error"),
                NotFoundException     => (StatusCodes.Status404NotFound, "Not Found"),
                AiParsingException    => (StatusCodes.Status422UnprocessableEntity, "AI Parsing Error"),
                HttpRequestException  => (StatusCodes.Status502BadGateway, "LLM Service Error"),
                _                     => (StatusCodes.Status500InternalServerError, "Internal Server Error")
            };

            var problem = new ProblemDetails
            {
                Status   = statusCode,
                Title    = title,
                Detail   = ex.Message,
                Instance = context.Request.Path,
                Type     = "https://tools.ietf.org/html/rfc7807"
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
