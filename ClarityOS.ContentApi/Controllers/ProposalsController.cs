using ClarityOS.ContentApi.Data.Entities;
using ClarityOS.ContentApi.Data.Repositories;
using ClarityOS.ContentApi.DTOs;
using ClarityOS.ContentApi.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ClarityOS.ContentApi.Controllers;

/// <summary>Manages AI-generated task proposals.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProposalsController(
    IProposalRepository proposalRepo,
    ITaskRepository taskRepo) : ControllerBase
{
    /// <summary>Returns all proposals.</summary>
    /// <returns>List of all AI proposals.</returns>
    /// <response code="200">Returns the list of proposals.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProposalResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var proposals = await proposalRepo.GetAllAsync();
        return Ok(proposals.Select(ToResponse).ToList());
    }

    /// <summary>Accepts a proposal and applies it to the linked task.</summary>
    /// <param name="id">The proposal GUID.</param>
    /// <returns>The updated task.</returns>
    /// <response code="200">Proposal accepted and task updated.</response>
    /// <response code="404">Proposal or linked task not found.</response>
    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(Guid id)
    {
        var proposal = await proposalRepo.GetByIdAsync(id)
            ?? throw new NotFoundException("Proposal not found");

        var task = await taskRepo.GetByIdAsync(proposal.OriginalTaskId)
            ?? throw new NotFoundException("Linked task not found");

        task.Title       = proposal.ProposedTitle;
        task.Description = proposal.ProposedDescription;
        task.DueDate     = proposal.ProposedDueDate;

        proposal.Status = ProposalStatus.Accepted;

        await taskRepo.UpdateAsync(task);
        await proposalRepo.UpdateAsync(proposal);

        return Ok(new TaskResponse(
            task.Id, task.Title, task.Description, task.Category,
            task.DueDate, task.IsCompleted, task.CreatedAt));
    }

    /// <summary>Rejects a proposal.</summary>
    /// <param name="id">The proposal GUID.</param>
    /// <returns>No content.</returns>
    /// <response code="200">Proposal rejected.</response>
    /// <response code="404">Proposal not found.</response>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id)
    {
        var proposal = await proposalRepo.GetByIdAsync(id)
            ?? throw new NotFoundException("Proposal not found");

        proposal.Status = ProposalStatus.Rejected;
        await proposalRepo.UpdateAsync(proposal);

        return Ok();
    }

    private static ProposalResponse ToResponse(AiProposal p) => new(
        p.Id, p.OriginalTaskId, p.ProposedTitle, p.ProposedDescription,
        p.ProposedDueDate, p.CreatedAt, p.Status);
}
