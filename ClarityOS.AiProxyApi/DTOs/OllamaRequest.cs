using System.Text.Json.Serialization;

namespace ClarityOS.AiProxyApi.DTOs;

internal record OllamaRequest(
    [property: JsonPropertyName("model")]  string Model,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("stream")] bool Stream = false
);
