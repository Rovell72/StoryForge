namespace StoryForge.Api.Features.Rounds;

public sealed record StartRoundCommand(Guid BranchId, int DurationMinutes) : IRequest<RoundDto>;

public sealed class StartRoundValidator : AbstractValidator<StartRoundCommand>
{
    public StartRoundValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("Идентификатор ветки обязателен");
        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 10080).WithMessage("Длительность раунда должна быть от 1 минуты до 7 суток");
    }
}

public sealed class StartRoundHandler(AppDbContext db, TimeProvider clock) : IRequestHandler<StartRoundCommand, RoundDto>
{
    public async Task<RoundDto> Handle(StartRoundCommand request, CancellationToken ct)
    {
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BranchId, ct)
                     ?? throw new NotFoundException($"Ветка {request.BranchId} не найдена");

        if (!branch.IsActive)
        {
            throw new ConflictException("Ветка неактивна, раунд открыть нельзя");
        }

        if (await db.Rounds.AnyAsync(r => r.BranchId == branch.Id && !r.IsClosed, ct))
        {
            throw new ConflictException("В ветке уже есть открытый раунд");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var round = new Round
        {
            BranchId = branch.Id,
            StartAt = now,
            EndAt = now.AddMinutes(request.DurationMinutes),
            IsClosed = false
        };

        db.Rounds.Add(round);
        await db.SaveChangesAsync(ct);
        return round.ToDto();
    }
}

public sealed record CloseRoundCommand(Guid RoundId) : IRequest<RoundDto>;

public sealed class CloseRoundHandler(AppDbContext db, RoundService rounds) : IRequestHandler<CloseRoundCommand, RoundDto>
{
    public async Task<RoundDto> Handle(CloseRoundCommand request, CancellationToken ct)
    {
        var round = await db.Rounds.FirstOrDefaultAsync(r => r.Id == request.RoundId, ct)
                    ?? throw new NotFoundException($"Раунд {request.RoundId} не найден");

        if (round.IsClosed)
        {
            throw new ConflictException("Раунд уже закрыт");
        }

        await rounds.CloseAsync(round, ct);
        return round.ToDto();
    }
}

public sealed record CloseExpiredRoundsCommand : IRequest<int>;

public sealed class CloseExpiredRoundsHandler(AppDbContext db, RoundService rounds, TimeProvider clock)
    : IRequestHandler<CloseExpiredRoundsCommand, int>
{
    public async Task<int> Handle(CloseExpiredRoundsCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.Rounds
            .AsNoTracking()
            .Where(r => !r.IsClosed && r.EndAt <= now)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var closed = 0;

        foreach (var id in ids)
        {
            try
            {
                var round = await db.Rounds.FirstOrDefaultAsync(r => r.Id == id, ct);
                if (round is null || round.IsClosed)
                {
                    continue;
                }

                await rounds.CloseAsync(round, ct);
                closed++;
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
            }
        }

        return closed;
    }
}

public sealed record GetBranchRoundsQuery(Guid BranchId) : IRequest<IReadOnlyList<RoundDto>>;

public sealed class GetBranchRoundsHandler(AppDbContext db) : IRequestHandler<GetBranchRoundsQuery, IReadOnlyList<RoundDto>>
{
    public async Task<IReadOnlyList<RoundDto>> Handle(GetBranchRoundsQuery request, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == request.BranchId, ct))
        {
            throw new NotFoundException($"Ветка {request.BranchId} не найдена");
        }

        var items = await db.Rounds
            .AsNoTracking()
            .Where(r => r.BranchId == request.BranchId)
            .OrderByDescending(r => r.StartAt)
            .ToListAsync(ct);

        return items.Select(r => r.ToDto()).ToList();
    }
}
