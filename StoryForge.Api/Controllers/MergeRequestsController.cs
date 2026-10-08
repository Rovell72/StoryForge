using Microsoft.AspNetCore.Mvc;

namespace StoryForge.Api.Controllers;

[ApiController]
[Route("api/merge-requests")]
public sealed class MergeRequestsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MergeProposalResultDto>> Propose(ProposeMergeCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return Created($"/api/merge-requests/{result.MergeRequest.Id}", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MergeRequestDto>> Get(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetMergeRequestQuery(id), ct));

    [HttpPost("{id:guid}/votes")]
    public async Task<ActionResult<MergeVoteResultDto>> Vote(Guid id, VoteRequest body, CancellationToken ct)
        => Ok(await sender.Send(new VoteMergeRequestCommand(id, body.VoterId, body.Value), ct));
}
