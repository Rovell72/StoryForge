namespace StoryForge.Api.Common;

public sealed class StoryOptions
{
    public const string SectionName = "Story";

    public int PromotionThreshold { get; set; } = 5;
    public int RejectionThreshold { get; set; } = 5;
    public int MergeVoteThreshold { get; set; } = 5;
    public int MinReputationToMerge { get; set; } = 20;
    public int ReputationPerPromotion { get; set; } = 10;
    public int ReputationPerMerge { get; set; } = 5;
    public int RoundCheckIntervalSeconds { get; set; } = 15;
}
