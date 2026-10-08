using Microsoft.AspNetCore.Mvc;

namespace StoryForge.Api.Controllers;

[ApiController]
[Route("api/contributions")]
public sealed class ContributionsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContributionDto>> Get(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetContributionQuery(id), ct));

    [HttpPost("{id:guid}/votes")]
    public async Task<ActionResult<VoteResultDto>> Vote(Guid id, VoteRequest body, CancellationToken ct)
        => Ok(await sender.Send(new VoteContributionCommand(id, body.VoterId, body.Value), ct));
}
