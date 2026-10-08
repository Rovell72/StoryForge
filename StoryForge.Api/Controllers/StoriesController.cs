using Microsoft.AspNetCore.Mvc;

namespace StoryForge.Api.Controllers;

[ApiController]
[Route("api/stories")]
public sealed class StoriesController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<StoryDetailsDto>> Create(CreateStoryCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<StoryDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await sender.Send(new GetStoriesQuery(page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StoryDetailsDto>> Get(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetStoryQuery(id), ct));
}
