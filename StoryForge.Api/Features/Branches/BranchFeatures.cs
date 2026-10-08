namespace StoryForge.Api.Features.Branches;

public sealed record GetBranchQuery(Guid Id) : IRequest<BranchDto>;

public sealed class GetBranchHandler(AppDbContext db) : IRequestHandler<GetBranchQuery, BranchDto>
{
    public async Task<BranchDto> Handle(GetBranchQuery request, CancellationToken ct)
    {
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.Id, ct)
                     ?? throw new NotFoundException($"Ветка {request.Id} не найдена");
        return branch.ToDto();
    }
}

public sealed record GetBranchTextQuery(Guid BranchId) : IRequest<BranchTextDto>;

public sealed class GetBranchTextHandler(AppDbContext db) : IRequestHandler<GetBranchTextQuery, BranchTextDto>
{
    public async Task<BranchTextDto> Handle(GetBranchTextQuery request, CancellationToken ct)
    {
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BranchId, ct)
                     ?? throw new NotFoundException($"Ветка {request.BranchId} не найдена");

        var parts = await db.Contributions
            .AsNoTracking()
            .Where(c => c.BranchId == branch.Id && c.Status == ContributionStatus.Accepted)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);

        var dtos = parts.Select(c => c.ToDto()).ToList();
        var fullText = string.Join("\n\n", parts.Select(c => c.Text));
        return new BranchTextDto(branch.Id, branch.Title, dtos, fullText);
    }
}

public sealed record ForkBranchCommand(Guid BranchId, Guid UserId, string Title) : IRequest<BranchDto>;

public sealed class ForkBranchValidator : AbstractValidator<ForkBranchCommand>
{
    public ForkBranchValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("Идентификатор ветки обязателен");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Идентификатор пользователя обязателен");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Название ветки обязательно")
            .MaximumLength(200).WithMessage("Название не должно превышать 200 символов");
    }
}

public sealed class ForkBranchHandler(AppDbContext db, TimeProvider clock) : IRequestHandler<ForkBranchCommand, BranchDto>
{
    public async Task<BranchDto> Handle(ForkBranchCommand request, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, ct))
        {
            throw new NotFoundException($"Пользователь {request.UserId} не найден");
        }

        var parent = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BranchId, ct)
                     ?? throw new NotFoundException($"Ветка {request.BranchId} не найдена");

        var now = clock.GetUtcNow().UtcDateTime;

        var fork = new Branch
        {
            Id = Guid.CreateVersion7(),
            StoryId = parent.StoryId,
            ParentBranchId = parent.Id,
            Title = request.Title,
            CreatedAt = now,
            IsActive = true
        };

        var accepted = await db.Contributions
            .AsNoTracking()
            .Where(c => c.BranchId == parent.Id && c.Status == ContributionStatus.Accepted)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);

        db.Branches.Add(fork);

        foreach (var item in accepted)
        {
            db.Contributions.Add(new Contribution
            {
                Id = Guid.CreateVersion7(),
                OriginId = item.OriginId,
                BranchId = fork.Id,
                Author = item.Author,
                Text = item.Text,
                CreatedAt = item.CreatedAt,
                Status = ContributionStatus.Accepted,
                PromotedAt = item.PromotedAt,
                Position = item.Position
            });
        }

        await db.SaveChangesAsync(ct);
        return fork.ToDto();
    }
}

public sealed record DeactivateBranchCommand(Guid BranchId) : IRequest<BranchDto>;

public sealed class DeactivateBranchHandler(AppDbContext db) : IRequestHandler<DeactivateBranchCommand, BranchDto>
{
    public async Task<BranchDto> Handle(DeactivateBranchCommand request, CancellationToken ct)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, ct)
                     ?? throw new NotFoundException($"Ветка {request.BranchId} не найдена");

        var story = await db.Stories.AsNoTracking().FirstAsync(s => s.Id == branch.StoryId, ct);
        if (story.RootBranchId == branch.Id)
        {
            throw new ConflictException("Корневую ветку нельзя деактивировать");
        }

        if (!branch.IsActive)
        {
            throw new ConflictException("Ветка уже неактивна");
        }

        branch.IsActive = false;

        var openRounds = await db.Rounds.Where(r => r.BranchId == branch.Id && !r.IsClosed).ToListAsync(ct);
        foreach (var round in openRounds)
        {
            round.IsClosed = true;
        }

        await db.SaveChangesAsync(ct);
        return branch.ToDto();
    }
}
