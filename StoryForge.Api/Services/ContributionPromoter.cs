namespace StoryForge.Api.Services;

public sealed class ContributionPromoter(AppDbContext db, IOptions<StoryOptions> options, TimeProvider clock)
{
    public async Task PromoteAsync(Contribution contribution, CancellationToken ct)
    {
        var last = await db.Contributions
            .Where(c => c.BranchId == contribution.BranchId && c.Position != null)
            .MaxAsync(c => c.Position, ct);

        contribution.Status = ContributionStatus.Accepted;
        contribution.PromotedAt = clock.GetUtcNow().UtcDateTime;
        contribution.Position = (last ?? 0) + 1;

        var author = await db.Users.FirstOrDefaultAsync(u => u.Username == contribution.Author, ct);
        if (author is not null)
        {
            author.Reputation += options.Value.ReputationPerPromotion;
        }
    }
}
