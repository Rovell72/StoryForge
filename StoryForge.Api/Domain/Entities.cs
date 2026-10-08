namespace StoryForge.Api.Domain;

public enum ContributionStatus
{
    Pending,
    Accepted,
    Rejected
}

public enum MergeRequestStatus
{
    Open,
    Merged,
    Rejected
}

public class UserProfile
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Username { get; set; } = string.Empty;
    public int Reputation { get; set; }
}

public class Story
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid RootBranchId { get; set; }
    public List<Branch> Branches { get; set; } = [];
}

public class Branch
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid StoryId { get; set; }
    public Guid? ParentBranchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Contribution
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OriginId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? RoundId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ContributionStatus Status { get; set; } = ContributionStatus.Pending;
    public int VotesUp { get; set; }
    public int VotesDown { get; set; }
    public DateTime? PromotedAt { get; set; }
    public int? Position { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public int Score => VotesUp - VotesDown;
}

public class Vote
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ContributionId { get; set; }
    public Guid VoterId { get; set; }
    public int Value { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Round
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsClosed { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class MergeRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SourceBranchId { get; set; }
    public Guid TargetBranchId { get; set; }
    public Guid ProposedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public MergeRequestStatus Status { get; set; } = MergeRequestStatus.Open;
    public int VotesUp { get; set; }
    public int VotesDown { get; set; }
    public DateTime? ClosedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public int Score => VotesUp - VotesDown;
}

public class MergeVote
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid MergeRequestId { get; set; }
    public Guid VoterId { get; set; }
    public int Value { get; set; }
    public DateTime CreatedAt { get; set; }
}
