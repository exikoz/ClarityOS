namespace ClarityOS.AiProxyApi.Exceptions;

public class RateLimitException(string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
