using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace StoryForge.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> Users => Set<UserProfile>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<MergeRequest> MergeRequests => Set<MergeRequest>();
    public DbSet<MergeVote> MergeVotes => Set<MergeVote>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>(e =>
        {
            e.ToTable("UserProfiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Reputation);
        });

        modelBuilder.Entity<Story>(e =>
        {
            e.ToTable("Stories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.HasMany(x => x.Branches).WithOne().HasForeignKey(x => x.StoryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Branch>(e =>
        {
            e.ToTable("Branches");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.ParentBranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.StoryId);
        });

        modelBuilder.Entity<Round>(e =>
        {
            e.ToTable("Rounds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.IsClosed, x.EndAt });
            e.HasIndex(x => x.BranchId);
        });

        modelBuilder.Entity<Contribution>(e =>
        {
            e.ToTable("Contributions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Author).HasMaxLength(64).IsRequired();
            e.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Round>().WithMany().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.BranchId, x.Status });
            e.HasIndex(x => x.RoundId);
            e.HasIndex(x => x.OriginId);
        });

        modelBuilder.Entity<Vote>(e =>
        {
            e.ToTable("Votes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne<Contribution>().WithMany().HasForeignKey(x => x.ContributionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.VoterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ContributionId, x.VoterId }).IsUnique();
        });

        modelBuilder.Entity<MergeRequest>(e =>
        {
            e.ToTable("MergeRequests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.SourceBranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.TargetBranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.ProposedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SourceBranchId, x.TargetBranchId, x.Status });
        });

        modelBuilder.Entity<MergeVote>(e =>
        {
            e.ToTable("MergeVotes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne<MergeRequest>().WithMany().HasForeignKey(x => x.MergeRequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.VoterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.MergeRequestId, x.VoterId }).IsUnique();
        });
    }
}

public sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

public sealed class NullableUtcDateTimeConverter()
    : ValueConverter<DateTime?, DateTime?>(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
