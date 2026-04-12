namespace ClarityOS.AiProxyApi.Middleware;

public class ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    private const string ApiKeyHeader = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Allow Scalar and OpenAPI routes through without a key
        if (path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var expectedKey = config["AiProxyApi:ApiKey"];

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("API key is not configured.");
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey)
            || providedKey != expectedKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: invalid or missing X-Api-Key header.");
            return;
        }

        await next(context);
    }
}
