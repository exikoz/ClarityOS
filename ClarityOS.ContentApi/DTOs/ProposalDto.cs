using System.Text.Json.Serialization;

namespace ClarityOS.ContentApi.DTOs;

// Internal DTO for parsing the raw JSON returned by the LLM
internal record ProposalDto(
    [property: JsonPropertyName("taskId")]              string TaskId,
    [property: JsonPropertyName("proposedTitle")]       string ProposedTitle,
    [property: JsonPropertyName("proposedDescription")] string? ProposedDescription,
    [property: JsonPropertyName("proposedDueDate")]     string ProposedDueDate
);
