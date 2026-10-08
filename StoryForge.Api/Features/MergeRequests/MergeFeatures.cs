namespace StoryForge.Api.Features.MergeRequests;

public sealed record ProposeMergeCommand(Guid SourceBranchId, Guid TargetBranchId, Guid ProposedById) : IRequest<MergeProposalResultDto>;

public sealed class ProposeMergeValidator : AbstractValidator<ProposeMergeCommand>
{
    public ProposeMergeValidator()
    {
        RuleFor(x => x.SourceBranchId).NotEmpty().WithMessage("Исходная ветка обязательна");
        RuleFor(x => x.TargetBranchId).NotEmpty().WithMessage("Целевая ветка обязательна");
        RuleFor(x => x.ProposedById).NotEmpty().WithMessage("Идентификатор пользователя обязателен");
        RuleFor(x => x).Must(x => x.SourceBranchId != x.TargetBranchId)
            .WithMessage("Исходная и целевая ветки должны отличаться");
    }
}

public sealed class ProposeMergeHandler(
    AppDbContext db,
    BranchMerger merger,
    IOptions<StoryOptions> options,
    TimeProvider clock) : IRequestHandler<ProposeMergeCommand, MergeProposalResultDto>
{
    public async Task<MergeProposalResultDto> Handle(ProposeMergeCommand request, CancellationToken ct)
    {
        var settings = options.Value;

        var proposer = await db.Users.FirstOrDefaultAsync(u => u.Id == request.ProposedById, ct)
                       ?? throw new NotFoundException($"Пользователь {request.ProposedById} не найден");

        if (proposer.Reputation < settings.MinReputationToMerge)
        {
            throw new ForbiddenException(
                $"Для предложения слияния нужна репутация не ниже {settings.MinReputationToMerge}, у вас {proposer.Reputation}");
        }

        var branches = await db.Branches
            .AsNoTracking()
            .Where(b => b.Id == request.SourceBranchId || b.Id == request.TargetBranchId)
            .ToListAsync(ct);

        var source = branches.FirstOrDefault(b => b.Id == request.SourceBranchId)
                     ?? throw new NotFoundException($"Ветка {request.SourceBranchId} не найдена");
        var target = branches.FirstOrDefault(b => b.Id == request.TargetBranchId)
                     ?? throw new NotFoundException($"Ветка {request.TargetBranchId} не найдена");

        if (source.StoryId != target.StoryId)
        {
            throw new ConflictException("Ветки принадлежат разным историям");
        }

        if (!target.IsActive)
        {
            throw new ConflictException("Целевая ветка неактивна");
        }

        if (await db.MergeRequests.AnyAsync(
                m => m.SourceBranchId == source.Id && m.TargetBranchId == target.Id && m.Status == MergeRequestStatus.Open, ct))
        {
            throw new ConflictException("Для этих веток уже есть открытый запрос на слияние");
        }

        var analysis = await merger.AnalyzeAsync(source.Id, target.Id, ct);

        if (analysis.Incoming.Count == 0)
        {
            throw new ConflictException("В исходной ветке нет новых принятых вкладов для слияния");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var mergeRequest = new MergeRequest
        {
            SourceBranchId = source.Id,
            TargetBranchId = target.Id,
            ProposedById = proposer.Id,
            CreatedAt = now,
            Status = MergeRequestStatus.Open
        };

        var merged = 0;

        if (!analysis.HasConflict)
        {
            merged = await merger.ApplyAsync(target.Id, analysis.Incoming, ct);
            mergeRequest.Status = MergeRequestStatus.Merged;
            mergeRequest.ClosedAt = now;
            proposer.Reputation += settings.ReputationPerMerge;
        }

        db.MergeRequests.Add(mergeRequest);
        await db.SaveChangesAsync(ct);

        return new MergeProposalResultDto(!analysis.HasConflict, merged, mergeRequest.ToDto());
    }
}

public sealed record VoteMergeRequestCommand(Guid MergeRequestId, Guid VoterId, int Value) : IRequest<MergeVoteResultDto>;

public sealed class VoteMergeRequestValidator : AbstractValidator<VoteMergeRequestCommand>
{
    public VoteMergeRequestValidator()
    {
        RuleFor(x => x.MergeRequestId).NotEmpty().WithMessage("Идентификатор запроса на слияние обязателен");
        RuleFor(x => x.VoterId).NotEmpty().WithMessage("Идентификатор голосующего обязателен");
        RuleFor(x => x.Value).Must(v => v is 1 or -1).WithMessage("Значение голоса должно быть +1 или -1");
    }
}

public sealed class VoteMergeRequestHandler(
    AppDbContext db,
    BranchMerger merger,
    IOptions<StoryOptions> options,
    TimeProvider clock) : IRequestHandler<VoteMergeRequestCommand, MergeVoteResultDto>
{
    public async Task<MergeVoteResultDto> Handle(VoteMergeRequestCommand request, CancellationToken ct)
    {
        var settings = options.Value;

        var mergeRequest = await db.MergeRequests.FirstOrDefaultAsync(m => m.Id == request.MergeRequestId, ct)
                           ?? throw new NotFoundException($"Запрос на слияние {request.MergeRequestId} не найден");

        if (mergeRequest.Status != MergeRequestStatus.Open)
        {
            throw new ConflictException("Голосование по этому запросу на слияние закрыто");
        }

        if (!await db.Users.AnyAsync(u => u.Id == request.VoterId, ct))
        {
            throw new NotFoundException($"Пользователь {request.VoterId} не найден");
        }

        if (mergeRequest.ProposedById == request.VoterId)
        {
            throw new ForbiddenException("Нельзя голосовать за собственный запрос на слияние");
        }

        var existing = await db.MergeVotes
            .FirstOrDefaultAsync(v => v.MergeRequestId == mergeRequest.Id && v.VoterId == request.VoterId, ct);

        if (existing is null)
        {
            db.MergeVotes.Add(new MergeVote
            {
                MergeRequestId = mergeRequest.Id,
                VoterId = request.VoterId,
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
                mergeRequest.VotesUp--;
            }
            else
            {
                mergeRequest.VotesDown--;
            }

            existing.Value = request.Value;
        }

        if (request.Value == 1)
        {
            mergeRequest.VotesUp++;
        }
        else
        {
            mergeRequest.VotesDown++;
        }

        var merged = 0;
        var now = clock.GetUtcNow().UtcDateTime;

        if (mergeRequest.Score >= settings.MergeVoteThreshold)
        {
            var target = await db.Branches.AsNoTracking().FirstAsync(b => b.Id == mergeRequest.TargetBranchId, ct);
            if (!target.IsActive)
            {
                throw new ConflictException("Целевая ветка неактивна, слияние невозможно");
            }

            var analysis = await merger.AnalyzeAsync(mergeRequest.SourceBranchId, mergeRequest.TargetBranchId, ct);
            merged = await merger.ApplyAsync(mergeRequest.TargetBranchId, analysis.Incoming, ct);

            mergeRequest.Status = MergeRequestStatus.Merged;
            mergeRequest.ClosedAt = now;

            var proposer = await db.Users.FirstOrDefaultAsync(u => u.Id == mergeRequest.ProposedById, ct);
            if (proposer is not null)
            {
                proposer.Reputation += settings.ReputationPerMerge;
            }
        }
        else if (mergeRequest.Score <= -settings.MergeVoteThreshold)
        {
            mergeRequest.Status = MergeRequestStatus.Rejected;
            mergeRequest.ClosedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return new MergeVoteResultDto(mergeRequest.ToDto(), merged);
    }
}

public sealed record GetMergeRequestQuery(Guid Id) : IRequest<MergeRequestDto>;

public sealed class GetMergeRequestHandler(AppDbContext db) : IRequestHandler<GetMergeRequestQuery, MergeRequestDto>
{
    public async Task<MergeRequestDto> Handle(GetMergeRequestQuery request, CancellationToken ct)
    {
        var item = await db.MergeRequests.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.Id, ct)
                   ?? throw new NotFoundException($"Запрос на слияние {request.Id} не найден");
        return item.ToDto();
    }
}

public sealed record GetBranchMergeRequestsQuery(Guid BranchId) : IRequest<IReadOnlyList<MergeRequestDto>>;

public sealed class GetBranchMergeRequestsHandler(AppDbContext db)
    : IRequestHandler<GetBranchMergeRequestsQuery, IReadOnlyList<MergeRequestDto>>
{
    public async Task<IReadOnlyList<MergeRequestDto>> Handle(GetBranchMergeRequestsQuery request, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == request.BranchId, ct))
        {
            throw new NotFoundException($"Ветка {request.BranchId} не найдена");
        }

        var items = await db.MergeRequests
            .AsNoTracking()
            .Where(m => m.SourceBranchId == request.BranchId || m.TargetBranchId == request.BranchId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        return items.Select(m => m.ToDto()).ToList();
    }
}
