namespace StoryForge.Api.Features.Users;

public sealed record CreateUserCommand(string Username) : IRequest<UserDto>;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Имя пользователя обязательно")
            .MaximumLength(64).WithMessage("Имя пользователя не должно превышать 64 символа")
            .Matches(@"^[\p{L}\p{N}_.\-]+$").WithMessage("Имя пользователя может содержать только буквы, цифры и символы _ . -");
    }
}

public sealed class CreateUserHandler(AppDbContext db) : IRequestHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Username == request.Username, ct))
        {
            throw new ConflictException($"Пользователь с именем \"{request.Username}\" уже существует");
        }

        var user = new UserProfile { Username = request.Username, Reputation = 0 };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }
}

public sealed record GetUserQuery(Guid Id) : IRequest<UserDto>;

public sealed class GetUserHandler(AppDbContext db) : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.Id, ct)
                   ?? throw new NotFoundException($"Пользователь {request.Id} не найден");
        return user.ToDto();
    }
}

public sealed record GetTopUsersQuery(int Count) : IRequest<IReadOnlyList<UserDto>>;

public sealed class GetTopUsersValidator : AbstractValidator<GetTopUsersQuery>
{
    public GetTopUsersValidator()
    {
        RuleFor(x => x.Count).InclusiveBetween(1, 100).WithMessage("Количество должно быть от 1 до 100");
    }
}

public sealed class GetTopUsersHandler(AppDbContext db) : IRequestHandler<GetTopUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> Handle(GetTopUsersQuery request, CancellationToken ct)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.Reputation)
            .ThenBy(u => u.Username)
            .Take(request.Count)
            .ToListAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }
}
