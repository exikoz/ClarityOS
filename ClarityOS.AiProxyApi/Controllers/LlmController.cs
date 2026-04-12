using ClarityOS.AiProxyApi.DTOs;
using ClarityOS.AiProxyApi.LlmClients;
using Microsoft.AspNetCore.Mvc;

namespace ClarityOS.AiProxyApi.Controllers;

/// <summary>Gateway to the external LLM for AI-powered task proposals.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LlmController(ILlmClient llmClient) : ControllerBase
{
    /// <summary>Returns the list of available models for this LLM provider.</summary>
    /// <returns>List of model identifiers.</returns>
    /// <response code="200">Returns available models.</response>
    [HttpGet("models")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public IActionResult GetModels() => Ok(llmClient.AvailableModels);

    /// <summary>Generates AI proposals for the provided tasks.</summary>
    /// <param name="request">List of tasks, user prompt, and optional model override.</param>
    /// <returns>Raw JSON array of proposals from the LLM.</returns>
    /// <response code="200">Returns the LLM-generated proposals.</response>
    /// <response code="401">Missing or invalid X-Api-Key header.</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Generate([FromBody] GenerateRequest request)
    {
        var taskList = string.Join("\n", request.Tasks.Select(t =>
            $"- taskId: \"{t.TaskId}\", title: \"{t.Title}\", description: \"{t.Description}\""));

        var systemPrompt = $$"""
            You are a task planning assistant. Today's date is {{DateTime.UtcNow:yyyy-MM-dd}}.
            You will be given a list of tasks, each with an exact taskId (a UUID).
            You MUST use the EXACT taskId values provided — do not change, shorten, or invent taskIds.
            Return ONLY a valid JSON array — no explanation, no markdown, no code fences.
            Each element must have exactly these fields:
            {"taskId": "<exact UUID from input>", "proposedTitle": "...", "proposedDescription": "...", "proposedDueDate": "YYYY-MM-DD"}
            The taskIds in your response must exactly match the taskIds listed below.
            All proposedDueDate values must be in the future relative to today.
            """;

        var userPrompt = $"Tasks:\n{taskList}\n\nUser instruction: {request.UserPrompt}";

        var (rawJson, modelUsed) = await llmClient.GenerateAsync(systemPrompt, userPrompt, request.Model);
        return Ok(new { model = modelUsed, response = rawJson });
    }
}
