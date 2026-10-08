namespace StoryForge.Api.Contracts;

public static class Mapping
{
    public static UserDto ToDto(this UserProfile u) => new(u.Id, u.Username, u.Reputation);

    public static StoryDto ToDto(this Story s) => new(s.Id, s.Title, s.Description, s.CreatedAt, s.RootBranchId);

    public static BranchDto ToDto(this Branch b) => new(b.Id, b.StoryId, b.ParentBranchId, b.Title, b.CreatedAt, b.IsActive);

    public static ContributionDto ToDto(this Contribution c) => new(
        c.Id, c.BranchId, c.RoundId, c.Author, c.Text, c.CreatedAt, c.Status,
        c.VotesUp, c.VotesDown, c.Score, c.PromotedAt, c.Position);

    public static RoundDto ToDto(this Round r) => new(r.Id, r.BranchId, r.StartAt, r.EndAt, r.IsClosed);

    public static MergeRequestDto ToDto(this MergeRequest m) => new(
        m.Id, m.SourceBranchId, m.TargetBranchId, m.ProposedById, m.CreatedAt,
        m.Status, m.VotesUp, m.VotesDown, m.Score, m.ClosedAt);
}
