using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPostRepository"/>.
/// </summary>
public class PostRepository(ApplicationDbContext db) : IPostRepository
{
    /// <inheritdoc />
    public Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Posts.FirstOrDefaultAsync(post => post.Id == id, ct);

    /// <inheritdoc />
    public Task<Post?> GetByIdWithTargetsAsync(Guid id, CancellationToken ct = default)
        => db.Posts
            .Include(post => post.Targets)
            .ThenInclude(target => target.SocialAccount)
            .Include(post => post.Video)
            .FirstOrDefaultAsync(post => post.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        PostStatus? status = null,
        Platform? platform = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var query = db.Posts
            .AsNoTracking()
            .Include(post => post.Targets)
            .Include(post => post.Video)
            .Where(post => post.WorkspaceId == workspaceId);

        if (status.HasValue)
        {
            query = query.Where(post => post.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(post => post.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(post => post.CreatedAt <= toDate.Value);
        }

        if (platform.HasValue)
        {
            query = query.Where(post => post.Targets.Any(target => target.Platform == platform.Value));
        }

        return await query
            .OrderByDescending(post => post.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<int> CountByWorkspaceIdForPeriodAsync(
        Guid workspaceId,
        DateTime fromInclusiveUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct = default)
        => db.Posts
            .AsNoTracking()
            .CountAsync(
                post => post.WorkspaceId == workspaceId &&
                        post.CreatedAt >= fromInclusiveUtc &&
                        post.CreatedAt < toExclusiveUtc,
                ct);

    /// <inheritdoc />
    public async Task<Post> AddAsync(Post post, CancellationToken ct = default)
    {
        await db.Posts.AddAsync(post, ct);
        return post;
    }

    /// <inheritdoc />
    public void Update(Post post)
        => db.Posts.Update(post);

    /// <inheritdoc />
    public void Remove(Post post)
        => db.Posts.Remove(post);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> GetScheduledPostsDueAsync(DateTime utcNow, CancellationToken ct = default)
        => await db.Posts
            .Include(post => post.Targets)
            .Where(post =>
                post.Status == PostStatus.Scheduled &&
                post.Schedule.ScheduledAt <= utcNow)
            .OrderBy(post => post.Schedule.ScheduledAt)
            .ToListAsync(ct);
}
