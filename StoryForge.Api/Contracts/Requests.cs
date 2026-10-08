namespace StoryForge.Api.Contracts;

public sealed record ForkBranchRequest(Guid UserId, string Title);

public sealed record AddContributionRequest(Guid AuthorId, string Text);

public sealed record StartRoundRequest(int DurationMinutes);

public sealed record VoteRequest(Guid VoterId, int Value);
