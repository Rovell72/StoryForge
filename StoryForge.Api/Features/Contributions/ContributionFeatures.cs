namespace StoryForge.Api.Features.Contributions;

public sealed record AddContributionCommand(Guid BranchId, Guid AuthorId, string Text) : IRequest<ContributionDto>;

public sealed class AddContributionValidator : AbstractValidator<AddContributionCommand>
{
    public AddContributionValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("Идентификатор ветки обязателен");
        RuleFor(x => x.AuthorId).NotEmpty().WithMessage("Идентификатор автора обязателен");
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Текст вклада обязателен")
            .MaximumLength(2000).WithMessage("Текст вклада не должен превышать 2000 символов");
    }
}

public sealed class AddContributionHandler(AppDbContext db, RoundService rounds, TimeProvider clock)
    : IRequestHandler<AddContributionCommand, ContributionDto>
{
    public async Task<ContributionDto> Handle(AddContributionCommand request, CancellationToken ct)
    {
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BranchId, ct)
                     ?? throw new NotFoundException($"Ветка {request.BranchId} не найдена");

        if (!branch.IsActive)
        {
            throw new ConflictException("Ветка неактивна, добавлять вклады нельзя");
        }

        var author = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.AuthorId, ct)
                     ?? throw new NotFoundException($"Пользователь {request.AuthorId} не найден");

        var round = await rounds.EnsureOpenAsync(branch.Id, ct);

        var id = Guid.CreateVersion7();
        var contribution = new Contribution
        {
            Id = id,
            OriginId = id,
            BranchId = branch.Id,
            RoundId = round?.Id,
            Author = author.Username,
            Text = request.Text,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            Status = ContributionStatus.Pending
        };

        db.Contributions.Add(contribution);
        await db.SaveChangesAsync(ct);
        return contribution.ToDto();
    }
}

public sealed record GetContributionQuery(Guid Id) : IRequest<ContributionDto>;

public sealed class GetContributionHandler(AppDbContext db) : IRequestHandler<GetContributionQuery, ContributionDto>
{
    public async Task<ContributionDto> Handle(GetContributionQuery request, CancellationToken ct)
    {
        var contribution = await db.Contributions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.Id, ct)
                           ?? throw new NotFoundException($"Вклад {request.Id} не найден");
        return contribution.ToDto();
    }
}

public sealed record GetBranchContributionsQuery(Guid BranchId, ContributionStatus? Status) : IRequest<IReadOnlyList<ContributionDto>>;

public sealed class GetBranchContributionsHandler(AppDbContext db)
    : IRequestHandler<GetBranchContributionsQuery, IReadOnlyList<ContributionDto>>
{
    public async Task<IReadOnlyList<ContributionDto>> Handle(GetBranchContributionsQuery request, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == request.BranchId, ct))
        {
            throw new NotFoundException($"Ветка {request.BranchId} не найдена");
        }

        var query = db.Contributions.AsNoTracking().Where(c => c.BranchId == request.BranchId);

        if (request.Status is not null)
        {
            query = query.Where(c => c.Status == request.Status);
        }

        var ordered = request.Status == ContributionStatus.Accepted
            ? query.OrderBy(c => c.Position)
            : query.OrderByDescending(c => c.VotesUp - c.VotesDown).ThenBy(c => c.CreatedAt);

        var items = await ordered.ToListAsync(ct);
        return items.Select(c => c.ToDto()).ToList();
    }
}

public sealed record VoteContributionCommand(Guid ContributionId, Guid VoterId, int Value) : IRequest<VoteResultDto>;

public sealed class VoteContributionValidator : AbstractValidator<VoteContributionCommand>
{
    public VoteContributionValidator()
    {
        RuleFor(x => x.ContributionId).NotEmpty().WithMessage("Идентификатор вклада обязателен");
        RuleFor(x => x.VoterId).NotEmpty().WithMessage("Идентификатор голосующего обязателен");
        RuleFor(x => x.Value).Must(v => v is 1 or -1).WithMessage("Значение голоса должно быть +1 или -1");
    }
}

public sealed class VoteContributionHandler(
    AppDbContext db,
    RoundService rounds,
    ContributionPromoter promoter,
    IOptions<StoryOptions> options,
    TimeProvider clock) : IRequestHandler<VoteContributionCommand, VoteResultDto>
{
    public async Task<VoteResultDto> Handle(VoteContributionCommand request, CancellationToken ct)
    {
        var contribution = await db.Contributions.FirstOrDefaultAsync(c => c.Id == request.ContributionId, ct)
                           ?? throw new NotFoundException($"Вклад {request.ContributionId} не найден");

        if (contribution.Status != ContributionStatus.Pending)
        {
            throw new ConflictException("Голосовать можно только за вклады в статусе Pending");
        }

        var voter = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.VoterId, ct)
                    ?? throw new NotFoundException($"Пользователь {request.VoterId} не найден");

        if (string.Equals(voter.Username, contribution.Author, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Нельзя голосовать за собственный вклад");
        }

        var branch = await db.Branches.AsNoTracking().FirstAsync(b => b.Id == contribution.BranchId, ct);
        if (!branch.IsActive)
        {
            throw new ConflictException("Ветка неактивна, голосование недоступно");
        }

        await rounds.EnsureOpenAsync(contribution.BranchId, ct);

        var existing = await db.Votes.FirstOrDefaultAsync(v => v.ContributionId == contribution.Id && v.VoterId == voter.Id, ct);

        if (existing is null)
        {
            db.Votes.Add(new Vote
            {
                ContributionId = contribution.Id,
                VoterId = voter.Id,
                Value = request.Value,
                CreatedAt = clock.GetUtcNow().UtcDateTime
            });
        }
        else if (existing.Value == request.Value)
        {
            throw new ConflictException("Вы уже проголосовали таким образом");
        }
        else
        {
            if (existing.Value == 1)
            {
                contribution.VotesUp--;
            }
            else
            {
                contribution.VotesDown--;
            }

            existing.Value = request.Value;
        }

        if (request.Value == 1)
        {
            contribution.VotesUp++;
        }
        else
        {
            contribution.VotesDown++;
        }

        var promoted = false;
        var settings = options.Value;

        if (contribution.Score >= settings.PromotionThreshold)
        {
            await promoter.PromoteAsync(contribution, ct);
            promoted = true;
        }
        else if (contribution.Score <= -settings.RejectionThreshold)
        {
            contribution.Status = ContributionStatus.Rejected;
        }

        await db.SaveChangesAsync(ct);

        return new VoteResultDto(contribution.Id, contribution.VotesUp, contribution.VotesDown, contribution.Score, contribution.Status, promoted);
    }
}
