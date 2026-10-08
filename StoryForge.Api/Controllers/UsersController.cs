using Microsoft.AspNetCore.Mvc;

namespace StoryForge.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetUserQuery(id), ct));

    [HttpGet("top")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Top([FromQuery] int count = 10, CancellationToken ct = default)
        => Ok(await sender.Send(new GetTopUsersQuery(count), ct));
}
