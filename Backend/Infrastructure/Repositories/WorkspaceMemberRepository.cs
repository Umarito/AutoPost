using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWorkspaceMemberRepository"/>.
/// </summary>
public class WorkspaceMemberRepository(ApplicationDbContext db) : IWorkspaceMemberRepository
{
    /// <inheritdoc />
    public async Task<WorkspaceMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.WorkspaceMembers
            .Include(wm => wm.User)
            .FirstOrDefaultAsync(wm => wm.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkspaceMember>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => await db.WorkspaceMembers
            .AsNoTracking()
            .Include(wm => wm.User)
            .Where(wm => wm.WorkspaceId == workspaceId)
            .OrderBy(wm => wm.InvitedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkspaceMember>> GetPagedByWorkspaceIdAsync(Guid workspaceId, int skip, int take, CancellationToken ct = default)
        => await db.WorkspaceMembers
            .AsNoTracking()
            .Include(wm => wm.User)
            .Where(wm => wm.WorkspaceId == workspaceId)
            .OrderBy(wm => wm.InvitedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<WorkspaceMember?> GetByUserAndWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken ct = default)
        => await db.WorkspaceMembers
            .Include(wm => wm.User)
            .FirstOrDefaultAsync(wm => wm.UserId == userId && wm.WorkspaceId == workspaceId, ct);

    /// <inheritdoc />
    public async Task<WorkspaceMember?> GetByInvitedEmailAsync(Guid workspaceId, string invitedEmail, CancellationToken ct = default)
        => await db.WorkspaceMembers
            .Include(wm => wm.User)
            .FirstOrDefaultAsync(
                wm => wm.WorkspaceId == workspaceId && wm.InvitedEmail == invitedEmail,
                ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkspaceMember>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.WorkspaceMembers
            .AsNoTracking()
            .Include(wm => wm.Workspace)
            .Where(wm => wm.UserId == userId && wm.Status == MemberStatus.Active && wm.Workspace.IsActive)
            .OrderBy(wm => wm.JoinedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<int> CountByWorkspaceAsync(Guid workspaceId, MemberStatus? status = null, CancellationToken ct = default)
    {
        var query = db.WorkspaceMembers.AsNoTracking().Where(wm => wm.WorkspaceId == workspaceId);
        if (status.HasValue)
        {
            query = query.Where(wm => wm.Status == status.Value);
        }

        return query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<WorkspaceMember> AddAsync(WorkspaceMember member, CancellationToken ct = default)
    {
        await db.WorkspaceMembers.AddAsync(member, ct);
        return member;
    }

    /// <inheritdoc />
    public void Update(WorkspaceMember member)
        => db.WorkspaceMembers.Update(member);

    /// <inheritdoc />
    public void Remove(WorkspaceMember member)
        => db.WorkspaceMembers.Remove(member);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid userId, Guid workspaceId, CancellationToken ct = default)
        => db.WorkspaceMembers
            .AsNoTracking()
            .AnyAsync(wm => wm.UserId == userId && wm.WorkspaceId == workspaceId, ct);
}
