using Microsoft.AspNetCore.Mvc;

namespace StoryForge.Api.Controllers;

[ApiController]
[Route("api/rounds")]
public sealed class RoundsController(ISender sender) : ControllerBase
{
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<RoundDto>> Close(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new CloseRoundCommand(id), ct));
}
