namespace StoryForge.Api.Services;

public sealed record MergeAnalysis(IReadOnlyList<Contribution> Incoming, bool HasConflict);

public sealed class BranchMerger(AppDbContext db, TimeProvider clock)
{
    public async Task<MergeAnalysis> AnalyzeAsync(Guid sourceBranchId, Guid targetBranchId, CancellationToken ct)
    {
        var source = await db.Contributions
            .AsNoTracking()
            .Where(c => c.BranchId == sourceBranchId && c.Status == ContributionStatus.Accepted)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);

        var targetOrigins = (await db.Contributions
                .AsNoTracking()
                .Where(c => c.BranchId == targetBranchId && c.Status == ContributionStatus.Accepted)
                .Select(c => c.OriginId)
                .ToListAsync(ct))
            .ToHashSet();

        var sourceOrigins = source.Select(c => c.OriginId).ToHashSet();
        var incoming = source.Where(c => !targetOrigins.Contains(c.OriginId)).ToList();
        var targetOnly = targetOrigins.Count(o => !sourceOrigins.Contains(o));

        return new MergeAnalysis(incoming, incoming.Count > 0 && targetOnly > 0);
    }

    public async Task<int> ApplyAsync(Guid targetBranchId, IReadOnlyList<Contribution> incoming, CancellationToken ct)
    {
        if (incoming.Count == 0)
        {
            return 0;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var last = await db.Contributions
            .Where(c => c.BranchId == targetBranchId && c.Position != null)
            .MaxAsync(c => c.Position, ct);
        var position = last ?? 0;

        foreach (var item in incoming.OrderBy(c => c.Position))
        {
            db.Contributions.Add(new Contribution
            {
                Id = Guid.CreateVersion7(),
                OriginId = item.OriginId,
                BranchId = targetBranchId,
                Author = item.Author,
                Text = item.Text,
                CreatedAt = item.CreatedAt,
                Status = ContributionStatus.Accepted,
                PromotedAt = now,
                Position = ++position
            });
        }

        return incoming.Count;
    }
}
