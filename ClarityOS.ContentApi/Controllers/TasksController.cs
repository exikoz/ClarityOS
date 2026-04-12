using ClarityOS.ContentApi.Data.Entities;
using ClarityOS.ContentApi.Data.Repositories;
using ClarityOS.ContentApi.DTOs;
using ClarityOS.ContentApi.Exceptions;
using ClarityOS.ContentApi.Filters;
using ClarityOS.ContentApi.LlmProxy;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ValidationException = ClarityOS.ContentApi.Exceptions.ValidationException;

namespace ClarityOS.ContentApi.Controllers;

/// <summary>Manages ClarityTask resources.</summary>
[ApiController]
[Route("api/[controller]")]
[MeasureExecutionTime]
[Produces("application/json")]
public class TasksController(
    ITaskRepository repo,
    IProposalRepository proposalRepo,
    ILlmProxyClient llmProxyClient,
    ILogger<TasksController> logger) : ControllerBase
{
    /// <summary>Returns all tasks with optional filtering and sorting.</summary>
    /// <param name="category">Filter by category (e.g. school, career, health, admin).</param>
    /// <param name="startDate">Return only tasks with DueDate on or after this date.</param>
    /// <param name="sort">Sort field. Use createdAt for ascending or -createdAt for descending.</param>
    /// <returns>List of tasks matching the criteria.</returns>
    /// <response code="200">Returns the filtered list of tasks.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<TaskResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] DateTime? startDate,
        [FromQuery] string? sort)
    {
        var tasks = await repo.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(category))
            tasks = tasks.Where(t => t.Category == category);

        if (startDate.HasValue)
            tasks = tasks.Where(t => t.DueDate >= startDate.Value);

        tasks = sort switch
        {
            "-createdAt" => tasks.OrderByDescending(t => t.CreatedAt),
            "createdAt"  => tasks.OrderBy(t => t.CreatedAt),
            _            => tasks.OrderByDescending(t => t.CreatedAt)
        };

        return Ok(tasks.Select(ToResponse).ToList());
    }

    /// <summary>Returns a single task by ID.</summary>
    /// <param name="id">The task GUID.</param>
    /// <returns>The matching task.</returns>
    /// <response code="200">Task found.</response>
    /// <response code="404">Task not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var task = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException("Task not found");
        return Ok(ToResponse(task));
    }

    /// <summary>Creates a new task.</summary>
    /// <param name="request">Task creation payload.</param>
    /// <returns>The newly created task.</returns>
    /// <response code="201">Task created successfully.</response>
    /// <response code="400">Validation failed (e.g. DueDate in the past).</response>
    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
    {
        if (request.DueDate < DateTime.UtcNow)
            throw new ValidationException("DueDate cannot be in the past");

        var task = new ClarityTask
        {
            Id          = Guid.NewGuid(),
            Title       = request.Title,
            Description = request.Description,
            Category    = request.Category,
            DueDate     = request.DueDate,
            IsCompleted = false,
            CreatedAt   = DateTime.UtcNow
        };

        await repo.AddAsync(task);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, ToResponse(task));
    }

    /// <summary>Updates an existing task.</summary>
    /// <param name="id">The task GUID.</param>
    /// <param name="request">Task update payload.</param>
    /// <returns>The updated task.</returns>
    /// <response code="200">Task updated successfully.</response>
    /// <response code="404">Task not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request)
    {
        var task = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException("Task not found");

        task.Title       = request.Title;
        task.Description = request.Description;
        task.Category    = request.Category;
        task.DueDate     = request.DueDate;

        await repo.UpdateAsync(task);
        return Ok(ToResponse(task));
    }

    /// <summary>Deletes a task by ID.</summary>
    /// <param name="id">The task GUID.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Task deleted.</response>
    /// <response code="404">Task not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var task = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException("Task not found");

        await repo.DeleteAsync(task);
        return NoContent();
    }

    private static TaskResponse ToResponse(ClarityTask t) => new(
        t.Id, t.Title, t.Description, t.Category,
        t.DueDate, t.IsCompleted, t.CreatedAt);

    /// <summary>Triggers an AI rescheduling pass over all pending tasks.</summary>
    /// <param name="request">User prompt describing the rescheduling intent.</param>
    /// <returns>List of AI-generated proposals saved as pending.</returns>
    /// <response code="200">Proposals generated and saved.</response>
    /// <response code="400">AI returned an unparseable response.</response>
    [HttpPost("ai-reschedule")]
    [ProducesResponseType(typeof(List<ProposalResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AiReschedule([FromBody] AiRescheduleRequest request)
    {
        var allTasks = await repo.GetAllAsync();
        var pending  = allTasks.Where(t => !t.IsCompleted).ToList();
        var taskDtos = pending.Select(ToResponse).ToList();

        var (rawJson, modelUsed) = await llmProxyClient.RequestRescheduleAsync(taskDtos, request.UserPrompt);
        logger.LogInformation("Raw LLM response: {Raw}", rawJson);
        logger.LogInformation("Model used: {Model}", modelUsed);

        // Strip markdown code fences LLMs sometimes add
        var cleaned = rawJson.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence    = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }

        // Extract JSON array if buried inside a larger string
        var arrayStart = cleaned.IndexOf('[');
        var arrayEnd   = cleaned.LastIndexOf(']');
        if (arrayStart >= 0 && arrayEnd > arrayStart)
            cleaned = cleaned[arrayStart..(arrayEnd + 1)];

        // If LLM returned a single object instead of array, wrap it
        if (cleaned.TrimStart().StartsWith('{'))
            cleaned = $"[{cleaned}]";

        // Fix multiple objects without commas (LLM sometimes omits them)
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"\}\s*\{", "},{");

        logger.LogInformation("Cleaned LLM JSON: {Cleaned}", cleaned);

        List<ProposalDto>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<ProposalDto>>(cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            parsed = null;
        }

        if (parsed is null || parsed.Count == 0)
            throw new AiParsingException("The AI agent returned an invalid format");

        var proposals = new List<AiProposal>();
        foreach (var dto in parsed)
        {
            if (!Guid.TryParse(dto.TaskId, out var taskId))
            {
                logger.LogWarning("LLM returned invalid taskId: {TaskId}", dto.TaskId);
                continue;
            }
            if (!DateTime.TryParse(dto.ProposedDueDate, out var dueDate))
            {
                logger.LogWarning("LLM returned invalid proposedDueDate: {Date}", dto.ProposedDueDate);
                continue;
            }

            var proposal = new AiProposal
            {
                Id                  = Guid.NewGuid(),
                OriginalTaskId      = taskId,
                ProposedTitle       = dto.ProposedTitle,
                ProposedDescription = dto.ProposedDescription,
                ProposedDueDate     = dueDate,
                CreatedAt           = DateTime.UtcNow,
                Status              = ProposalStatus.Pending
            };

            await proposalRepo.AddAsync(proposal);
            proposals.Add(proposal);
        }

        var responses = proposals.Select(p => new ProposalResponse(
            p.Id, p.OriginalTaskId, p.ProposedTitle, p.ProposedDescription,
            p.ProposedDueDate, p.CreatedAt, p.Status)).ToList();

        return Ok(new { model = modelUsed, proposals = responses });
    }
}
