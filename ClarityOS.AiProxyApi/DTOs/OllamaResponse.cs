using System.Text.Json.Serialization;

namespace ClarityOS.AiProxyApi.DTOs;

internal record OllamaResponse(
    [property: JsonPropertyName("response")] string Response
);
