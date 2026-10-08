namespace StoryForge.Api.Contracts;

public sealed record UserDto(Guid Id, string Username, int Reputation);

public sealed record StoryDto(Guid Id, string Title, string Description, DateTime CreatedAt, Guid RootBranchId);

public sealed record StoryDetailsDto(
    Guid Id,
    string Title,
    string Description,
    DateTime CreatedAt,
    Guid RootBranchId,
    IReadOnlyList<BranchDto> Branches);

public sealed record BranchDto(
    Guid Id,
    Guid StoryId,
    Guid? ParentBranchId,
    string Title,
    DateTime CreatedAt,
    bool IsActive);

public sealed record BranchTextDto(
    Guid BranchId,
    string Title,
    IReadOnlyList<ContributionDto> Parts,
    string FullText);

public sealed record ContributionDto(
    Guid Id,
    Guid BranchId,
    Guid? RoundId,
    string Author,
    string Text,
    DateTime CreatedAt,
    ContributionStatus Status,
    int VotesUp,
    int VotesDown,
    int Score,
    DateTime? PromotedAt,
    int? Position);

public sealed record RoundDto(Guid Id, Guid BranchId, DateTime StartAt, DateTime EndAt, bool IsClosed);

public sealed record VoteResultDto(
    Guid ContributionId,
    int VotesUp,
    int VotesDown,
    int Score,
    ContributionStatus Status,
    bool Promoted);

public sealed record MergeRequestDto(
    Guid Id,
    Guid SourceBranchId,
    Guid TargetBranchId,
    Guid ProposedById,
    DateTime CreatedAt,
    MergeRequestStatus Status,
    int VotesUp,
    int VotesDown,
    int Score,
    DateTime? ClosedAt);

public sealed record MergeProposalResultDto(bool Merged, int MergedContributions, MergeRequestDto MergeRequest);

public sealed record MergeVoteResultDto(MergeRequestDto MergeRequest, int MergedContributions);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
