using Microsoft.AspNetCore.Mvc;

namespace StoryForge.Api.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BranchDto>> Get(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetBranchQuery(id), ct));

    [HttpGet("{id:guid}/text")]
    public async Task<ActionResult<BranchTextDto>> Text(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetBranchTextQuery(id), ct));

    [HttpPost("{id:guid}/fork")]
    public async Task<ActionResult<BranchDto>> Fork(Guid id, ForkBranchRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new ForkBranchCommand(id, body.UserId, body.Title), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<BranchDto>> Deactivate(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new DeactivateBranchCommand(id), ct));

    [HttpGet("{id:guid}/contributions")]
    public async Task<ActionResult<IReadOnlyList<ContributionDto>>> Contributions(
        Guid id, [FromQuery] ContributionStatus? status, CancellationToken ct)
        => Ok(await sender.Send(new GetBranchContributionsQuery(id, status), ct));

    [HttpPost("{id:guid}/contributions")]
    public async Task<ActionResult<ContributionDto>> AddContribution(Guid id, AddContributionRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new AddContributionCommand(id, body.AuthorId, body.Text), ct);
        return Created($"/api/contributions/{result.Id}", result);
    }

    [HttpGet("{id:guid}/rounds")]
    public async Task<ActionResult<IReadOnlyList<RoundDto>>> Rounds(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetBranchRoundsQuery(id), ct));

    [HttpPost("{id:guid}/rounds")]
    public async Task<ActionResult<RoundDto>> StartRound(Guid id, StartRoundRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new StartRoundCommand(id, body.DurationMinutes), ct);
        return Created($"/api/branches/{id}/rounds", result);
    }

    [HttpGet("{id:guid}/merge-requests")]
    public async Task<ActionResult<IReadOnlyList<MergeRequestDto>>> MergeRequests(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetBranchMergeRequestsQuery(id), ct));
}
