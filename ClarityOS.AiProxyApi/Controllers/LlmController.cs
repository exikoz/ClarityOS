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

            ## Rules
            1. You will receive a list of tasks, each with an exact taskId (UUID). You MUST use the EXACT taskId values provided. Never change, shorten, or invent taskIds.
            2. All proposedDueDate values MUST be in the future relative to today.
            3. Keep proposedTitle under 100 characters.
            4. Keep proposedDescription under 300 characters. Be concise and actionable.
            5. If you are unsure about a scheduling decision, state your uncertainty in the proposedDescription field.
            6. Do NOT invent new tasks. Only reschedule the tasks provided.
            7. Do NOT include any explanation, markdown, or code fences in your output.

            ## Output Format
            Return ONLY a valid JSON array. Each element must have exactly these fields:
            {"taskId": "<exact UUID from input>", "proposedTitle": "...", "proposedDescription": "...", "proposedDueDate": "YYYY-MM-DD"}

            ## Example
            Input task: taskId "a1b2c3d4-0000-0000-0000-000000000001", title "Buy groceries", description "Weekly shopping"
            User instruction: "Move everything to next Monday"

            Expected output:
            [{"taskId": "a1b2c3d4-0000-0000-0000-000000000001", "proposedTitle": "Buy groceries", "proposedDescription": "Weekly shopping, rescheduled to Monday", "proposedDueDate": "2026-06-08"}]
            """;

        var userPrompt = $"Tasks:\n{taskList}\n\nUser instruction: {request.UserPrompt}";

        var (rawJson, modelUsed) = await llmClient.GenerateAsync(systemPrompt, userPrompt, request.Model);
        return Ok(new { model = modelUsed, response = rawJson });
    }
}
