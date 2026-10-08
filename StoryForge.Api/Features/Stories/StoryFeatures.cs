namespace StoryForge.Api.Features.Stories;

public sealed record CreateStoryCommand(string Title, string Description) : IRequest<StoryDetailsDto>;

public sealed class CreateStoryValidator : AbstractValidator<CreateStoryCommand>
{
    public CreateStoryValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Название истории обязательно")
            .MaximumLength(200).WithMessage("Название не должно превышать 200 символов");
        RuleFor(x => x.Description)
            .NotNull().WithMessage("Описание обязательно")
            .MaximumLength(2000).WithMessage("Описание не должно превышать 2000 символов");
    }
}

public sealed class CreateStoryHandler(AppDbContext db, TimeProvider clock) : IRequestHandler<CreateStoryCommand, StoryDetailsDto>
{
    public async Task<StoryDetailsDto> Handle(CreateStoryCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var storyId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();

        var story = new Story
        {
            Id = storyId,
            Title = request.Title,
            Description = request.Description,
            CreatedAt = now,
            RootBranchId = branchId
        };

        var branch = new Branch
        {
            Id = branchId,
            StoryId = storyId,
            ParentBranchId = null,
            Title = "Основная ветка",
            CreatedAt = now,
            IsActive = true
        };

        db.Stories.Add(story);
        db.Branches.Add(branch);
        await db.SaveChangesAsync(ct);

        return new StoryDetailsDto(story.Id, story.Title, story.Description, story.CreatedAt, story.RootBranchId, [branch.ToDto()]);
    }
}

public sealed record GetStoriesQuery(int Page, int PageSize) : IRequest<PagedResult<StoryDto>>;

public sealed class GetStoriesValidator : AbstractValidator<GetStoriesQuery>
{
    public GetStoriesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Номер страницы должен быть не меньше 1");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Размер страницы должен быть от 1 до 100");
    }
}

public sealed class GetStoriesHandler(AppDbContext db) : IRequestHandler<GetStoriesQuery, PagedResult<StoryDto>>
{
    public async Task<PagedResult<StoryDto>> Handle(GetStoriesQuery request, CancellationToken ct)
    {
        var total = await db.Stories.CountAsync(ct);
        var stories = await db.Stories
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<StoryDto>(stories.Select(s => s.ToDto()).ToList(), request.Page, request.PageSize, total);
    }
}

public sealed record GetStoryQuery(Guid Id) : IRequest<StoryDetailsDto>;

public sealed class GetStoryHandler(AppDbContext db) : IRequestHandler<GetStoryQuery, StoryDetailsDto>
{
    public async Task<StoryDetailsDto> Handle(GetStoryQuery request, CancellationToken ct)
    {
        var story = await db.Stories
            .AsNoTracking()
            .Include(s => s.Branches)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new NotFoundException($"История {request.Id} не найдена");

        var branches = story.Branches.OrderBy(b => b.CreatedAt).Select(b => b.ToDto()).ToList();
        return new StoryDetailsDto(story.Id, story.Title, story.Description, story.CreatedAt, story.RootBranchId, branches);
    }
}
