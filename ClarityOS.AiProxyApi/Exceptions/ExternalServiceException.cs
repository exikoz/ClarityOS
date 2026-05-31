namespace ClarityOS.AiProxyApi.Exceptions;

public class ExternalServiceException(string message, int? upstreamStatusCode = null) : Exception(message)
{
    public int? UpstreamStatusCode { get; } = upstreamStatusCode;
}
