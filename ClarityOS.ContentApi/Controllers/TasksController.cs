using ClarityOS.ContentApi.Data.Entities;
using ClarityOS.ContentApi.Data.Repositories;
using ClarityOS.ContentApi.DTOs;
using ClarityOS.ContentApi.Filters;
using Microsoft.AspNetCore.Mvc;
using ValidationException = ClarityOS.ContentApi.Exceptions.ValidationException;

namespace ClarityOS.ContentApi.Controllers;

/// <summary>Manages ClarityTask resources.</summary>
[ApiController]
[Route("api/[controller]")]
[MeasureExecutionTime]
[Produces("application/json")]
public class TasksController(ITaskRepository repo) : ControllerBase
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
        var task = await repo.GetByIdAsync(id);
        return task is null ? NotFound() : Ok(ToResponse(task));
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
        var task = await repo.GetByIdAsync(id);
        if (task is null) return NotFound();

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
        var task = await repo.GetByIdAsync(id);
        if (task is null) return NotFound();

        await repo.DeleteAsync(task);
        return NoContent();
    }

    private static TaskResponse ToResponse(ClarityTask t) => new(
        t.Id, t.Title, t.Description, t.Category,
        t.DueDate, t.IsCompleted, t.CreatedAt);
}
