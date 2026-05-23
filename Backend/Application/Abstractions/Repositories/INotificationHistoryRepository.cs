using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Определяет контракт доступа к persisted operational-истории пользовательских уведомлений.
/// История нужна для notification center, аудита каналов и последующей диагностики доставки.
/// </summary>
public interface INotificationHistoryRepository
{
    /// <summary>
    /// Добавляет новую запись истории уведомления.
    /// </summary>
    Task<NotificationHistory> AddAsync(NotificationHistory history, CancellationToken ct = default);

    /// <summary>
    /// Помечает запись истории как измененную.
    /// </summary>
    void Update(NotificationHistory history);

    /// <summary>
    /// Возвращает страницу истории уведомлений конкретного пользователя в рамках конкретного workspace.
    /// </summary>
    Task<IReadOnlyList<NotificationHistory>> GetPagedByUserAndWorkspaceAsync(
        Guid userId,
        Guid workspaceId,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Подсчитывает количество записей истории уведомлений конкретного пользователя в текущем workspace.
    /// </summary>
    Task<int> CountByUserAndWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken ct = default);
}
