namespace StoryForge.Api.Services;

public sealed class RoundService(AppDbContext db, ContributionPromoter promoter, TimeProvider clock)
{
    public async Task<Round?> EnsureOpenAsync(Guid branchId, CancellationToken ct)
    {
        var round = await db.Rounds
            .AsNoTracking()
            .Where(r => r.BranchId == branchId && !r.IsClosed)
            .OrderByDescending(r => r.StartAt)
            .FirstOrDefaultAsync(ct);

        if (round is null)
        {
            return null;
        }

        if (clock.GetUtcNow().UtcDateTime >= round.EndAt)
        {
            throw new ConflictException("Время раунда истекло, ожидается подведение итогов");
        }

        return round;
    }

    public async Task CloseAsync(Round round, CancellationToken ct)
    {
        var pending = await db.Contributions
            .Where(c => c.RoundId == round.Id && c.Status == ContributionStatus.Pending)
            .ToListAsync(ct);

        var best = pending
            .Where(c => c.Score > 0)
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.VotesUp)
            .ThenBy(c => c.CreatedAt)
            .FirstOrDefault();

        foreach (var contribution in pending.Where(c => c != best))
        {
            contribution.Status = ContributionStatus.Rejected;
        }

        if (best is not null)
        {
            await promoter.PromoteAsync(best, ct);
        }

        round.IsClosed = true;
        await db.SaveChangesAsync(ct);
    }
}
