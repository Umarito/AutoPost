using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// Реализует persistence для истории пользовательских уведомлений.
/// История хранится отдельно, чтобы read-heavy notification center не зависел от runtime-состояния очередей доставки.
/// </summary>
public sealed class NotificationHistoryRepository(ApplicationDbContext db) : INotificationHistoryRepository
{
    /// <inheritdoc />
    public async Task<NotificationHistory> AddAsync(NotificationHistory history, CancellationToken ct = default)
    {
        await db.NotificationHistories.AddAsync(history, ct);
        return history;
    }

    /// <inheritdoc />
    public void Update(NotificationHistory history)
        => db.NotificationHistories.Update(history);

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationHistory>> GetPagedByUserAndWorkspaceAsync(
        Guid userId,
        Guid workspaceId,
        int skip,
        int take,
        CancellationToken ct = default)
        => await db.NotificationHistories.AsNoTracking()
            .Where(history => history.UserId == userId && history.WorkspaceId == workspaceId)
            .OrderByDescending(history => history.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<int> CountByUserAndWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken ct = default)
        => db.NotificationHistories.AsNoTracking()
            .CountAsync(history => history.UserId == userId && history.WorkspaceId == workspaceId, ct);
}
