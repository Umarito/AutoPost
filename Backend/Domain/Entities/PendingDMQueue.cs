using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Очередь отложенных direct messages.
/// Сущность хранит сообщения, которые нельзя отправить немедленно из-за ограничений платформы или состояния внешнего пользователя.
/// </summary>
public class PendingDMQueue : BaseEntity<Guid>
{
    /// <summary>
    /// Создает новую запись очереди отложенного direct message.
    /// </summary>
    /// <param name="automationRuleId">Идентификатор правила-инициатора.</param>
    /// <param name="socialAccountId">Идентификатор аккаунта-отправителя.</param>
    /// <param name="externalUserId">Идентификатор целевого внешнего пользователя.</param>
    /// <param name="externalUserName">Имя внешнего пользователя.</param>
    /// <param name="resolvedMessageText">Подготовленный текст сообщения.</param>
    /// <param name="reason">Причина отложенной отправки.</param>
    /// <param name="triggeredAtUtc">UTC-время триггера.</param>
    /// <param name="expiresAtUtc">UTC-время истечения срока ожидания.</param>
    /// <returns>Новая запись очереди.</returns>
    public static PendingDMQueue Create(
        Guid automationRuleId,
        Guid socialAccountId,
        string externalUserId,
        string? externalUserName,
        string resolvedMessageText,
        PendingReason reason,
        DateTime triggeredAtUtc,
        DateTime expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedMessageText);

        return new PendingDMQueue
        {
            Id = Guid.NewGuid(),
            AutomationRuleId = automationRuleId,
            SocialAccountId = socialAccountId,
            ExternalUserId = externalUserId.Trim(),
            ExternalUserName = string.IsNullOrWhiteSpace(externalUserName) ? null : externalUserName.Trim(),
            ResolvedMessageText = resolvedMessageText.Trim(),
            Reason = reason,
            TriggeredAt = triggeredAtUtc,
            ExpiresAt = expiresAtUtc,
            Status = PendingDMStatus.Waiting,
            CheckAttemptCount = 0
        };
    }

    /// <summary>
    /// Идентификатор правила автоматизации, создавшего отложенную отправку.
    /// Поле помогает проследить происхождение сообщения и понять, какой сценарий его породил.
    /// </summary>
    public Guid AutomationRuleId { get; private set; }

    /// <summary>
    /// Идентификатор социального аккаунта, от имени которого должен быть отправлен DM.
    /// Значение задает конкретный внешний канал для последующей доставки сообщения.
    /// </summary>
    public Guid SocialAccountId { get; private set; }

    /// <summary>
    /// Идентификатор внешнего пользователя, который должен получить сообщение.
    /// Поле связывает запись очереди с конкретным получателем на стороне платформы.
    /// </summary>
    public string ExternalUserId { get; private set; } = default!;

    /// <summary>
    /// Username внешнего пользователя на момент постановки в очередь.
    /// Значение упрощает работу команды с мониторингом pending-сообщений.
    /// </summary>
    public string? ExternalUserName { get; private set; }

    /// <summary>
    /// Финальный текст сообщения после разрешения всех шаблонов и подстановок.
    /// Поле хранит уже подготовленное содержимое, которое должно быть отправлено при выполнении условий.
    /// </summary>
    public string ResolvedMessageText { get; private set; } = default!;

    /// <summary>
    /// Причина, по которой сообщение не было отправлено сразу.
    /// Значение объясняет, какое внешнее ограничение заставило систему перевести действие в режим ожидания.
    /// </summary>
    public PendingReason Reason { get; private set; }

    /// <summary>
    /// Момент изначального срабатывания триггера, породившего сообщение.
    /// Эта временная метка отражает, когда бизнес-событие потребовало отправки DM.
    /// </summary>
    public DateTime TriggeredAt { get; private set; }

    /// <summary>
    /// Время последней проверки условий, необходимых для отправки сообщения.
    /// Поле помогает отслеживать работу фонового процесса, который повторно оценивает возможность доставки.
    /// </summary>
    public DateTime? LastCheckedAt { get; private set; }

    /// <summary>
    /// Количество выполненных проверок условий отправки.
    /// Значение полезно для диагностики затянувшихся ожиданий и анализа нагрузки на фоновые процессы.
    /// </summary>
    public int CheckAttemptCount { get; private set; }

    /// <summary>
    /// Время истечения срока ожидания отправки в UTC.
    /// После этого момента система должна считать запись просроченной и прекращать попытки автоматической доставки.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Текущее состояние записи очереди.
    /// Поле показывает, ожидает ли сообщение отправки, уже было доставлено или утратило актуальность.
    /// </summary>
    public PendingDMStatus Status { get; private set; }

    /// <summary>
    /// Правило автоматизации, создавшее отложенное сообщение.
    /// Навигация позволяет быстро перейти от очереди ожидания к исходной бизнес-настройке.
    /// </summary>
    public AutomationRule AutomationRule { get; private set; } = default!;

    /// <summary>
    /// Социальный аккаунт, с которого должно быть отправлено сообщение после снятия ограничения.
    /// Навигация связывает ожидание доставки с конкретной внешней интеграцией.
    /// </summary>
    public SocialAccount SocialAccount { get; private set; } = default!;

    /// <summary>
    /// Фиксирует очередную проверку условий доставки.
    /// </summary>
    /// <param name="checkedAtUtc">UTC-время проверки.</param>
    public void MarkChecked(DateTime checkedAtUtc)
    {
        LastCheckedAt = checkedAtUtc;
        CheckAttemptCount++;
    }

    /// <summary>
    /// Помечает запись очереди как успешно доставленную.
    /// </summary>
    /// <param name="sentAtUtc">UTC-время отправки.</param>
    public void MarkSent(DateTime sentAtUtc)
    {
        LastCheckedAt = sentAtUtc;
        Status = PendingDMStatus.Sent;
    }

    /// <summary>
    /// Помечает запись очереди как просроченную.
    /// </summary>
    /// <param name="expiredAtUtc">UTC-время фиксации истечения.</param>
    public void MarkExpired(DateTime expiredAtUtc)
    {
        LastCheckedAt = expiredAtUtc;
        Status = PendingDMStatus.Expired;
    }

    /// <summary>
    /// Отменяет отложенную отправку вручную.
    /// </summary>
    /// <param name="cancelledAtUtc">UTC-время отмены.</param>
    public void MarkCancelled(DateTime cancelledAtUtc)
    {
        LastCheckedAt = cancelledAtUtc;
        Status = PendingDMStatus.Cancelled;
    }
}
