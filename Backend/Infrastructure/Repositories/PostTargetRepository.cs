using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPostTargetRepository"/>.
///
/// <para><b>How it works:</b>
/// PostTarget is a child entity of the Post aggregate. All queries eagerly load the
/// SocialAccount navigation because the UI always shows which platform/account a target belongs to.</para>
///
/// <para><b>Purpose:</b>
/// Tracks per-platform publishing status within a post — used by the publisher job
/// and the post detail page.</para>
/// </summary>
public class PostTargetRepository(ApplicationDbContext db) : IPostTargetRepository
{
    /// <summary>
    /// Loads a post target with its SocialAccount. Tracked for status/result updates.
    /// </summary>
    public async Task<PostTarget?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.PostTargets.AsNoTracking()
            .Include(pt => pt.SocialAccount)
            .FirstOrDefaultAsync(pt => pt.Id == id, ct);

    /// <summary>
    /// Lists all targets for a post with their SocialAccount details.
    /// AsNoTracking — the post detail page per-platform status breakdown is read-only.
    /// </summary>
    public async Task<IReadOnlyList<PostTarget>> GetByPostIdAsync(Guid postId, CancellationToken ct = default)
        => await db.PostTargets.AsNoTracking()
            .Include(pt => pt.SocialAccount)
            .Where(pt => pt.PostId == postId)
            .ToListAsync(ct);

    /// <summary>
    /// Adds a new post target to the change tracker. Actual INSERT on SaveChangesAsync.
    /// </summary>
    public async Task<PostTarget> AddAsync(PostTarget target, CancellationToken ct = default)
    {
        await db.PostTargets.AddAsync(target, ct);
        return target;
    }

    /// <summary>
    /// Marks the target as Modified for status transitions and result population.
    /// </summary>
    public void Update(PostTarget target)
        => db.PostTargets.Update(target);
}
