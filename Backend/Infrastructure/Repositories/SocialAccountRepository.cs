using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISocialAccountRepository"/>.
/// </summary>
public class SocialAccountRepository(ApplicationDbContext db) : ISocialAccountRepository
{
    /// <inheritdoc />
    public Task<SocialAccount?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.SocialAccounts.AsNoTracking().FirstOrDefaultAsync(sa => sa.Id == id, ct);

    /// <inheritdoc />
    public Task<SocialAccount?> GetByIdWithWorkspaceAsync(Guid id, CancellationToken ct = default)
        => db.SocialAccounts.AsNoTracking()
            .Include(sa => sa.Workspace)
            .FirstOrDefaultAsync(sa => sa.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SocialAccount>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => await db.SocialAccounts
            .AsNoTracking()
            .Where(sa => sa.WorkspaceId == workspaceId)
            .OrderBy(sa => sa.ConnectedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<SocialAccount?> GetByExternalIdAsync(Guid workspaceId, Platform platform, string externalAccountId, CancellationToken ct = default)
        => db.SocialAccounts.AsNoTracking()
            .FirstOrDefaultAsync(
                sa => sa.WorkspaceId == workspaceId &&
                      sa.Platform == platform &&
                      sa.ExternalAccountId == externalAccountId,
                ct);

    /// <inheritdoc />
    public Task<int> CountActiveByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => db.SocialAccounts
            .AsNoTracking()
            .CountAsync(sa => sa.WorkspaceId == workspaceId && sa.Status == SocialAccountStatus.Active, ct);

    /// <inheritdoc />
    public async Task<SocialAccount> AddAsync(SocialAccount account, CancellationToken ct = default)
    {
        await db.SocialAccounts.AddAsync(account, ct);
        return account;
    }

    /// <inheritdoc />
    public void Update(SocialAccount account)
        => db.SocialAccounts.Update(account);

    /// <inheritdoc />
    public void Remove(SocialAccount account)
        => db.SocialAccounts.Remove(account);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SocialAccount>> GetByStatusAsync(SocialAccountStatus status, CancellationToken ct = default)
        => await db.SocialAccounts
            .AsNoTracking()
            .Where(sa => sa.Status == status)
            .OrderBy(sa => sa.ConnectedAt)
            .ToListAsync(ct);
}
