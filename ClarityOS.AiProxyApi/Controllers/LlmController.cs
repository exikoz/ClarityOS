using ClarityOS.AiProxyApi.DTOs;
using ClarityOS.AiProxyApi.LlmClients;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ClarityOS.AiProxyApi.Controllers;

/// <summary>Gateway to the external LLM for AI-powered task proposals.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LlmController(ILlmClient llmClient) : ControllerBase
{
    /// <summary>Generates AI proposals for the provided tasks.</summary>
    /// <param name="request">List of tasks and an optional user prompt.</param>
    /// <returns>Raw JSON array of proposals from the LLM.</returns>
    /// <response code="200">Returns the LLM-generated proposals as a JSON string.</response>
    /// <response code="401">Missing or invalid X-Api-Key header.</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Generate([FromBody] GenerateRequest request)
    {
        var taskList = string.Join("\n", request.Tasks.Select(t =>
            $"- id: {t.TaskId}, title: {t.Title}, description: {t.Description}"));

        var systemPrompt = """
            You are a task planning assistant. Given a list of tasks, return ONLY valid JSON — no explanation, no markdown.
            The JSON must be an array with this exact shape:
            [{"taskId": "...", "proposedTitle": "...", "proposedDescription": "...", "proposedDueDate": "YYYY-MM-DD"}]
            Do not include any text outside the JSON array.
            """;

        var userPrompt = $"Tasks:\n{taskList}\n\nUser instruction: {request.UserPrompt}";

        var rawJson = await llmClient.GenerateAsync(systemPrompt, userPrompt);
        return Ok(rawJson);
    }
}
