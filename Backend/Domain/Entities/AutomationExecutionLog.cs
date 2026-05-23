using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Журнал исполнения правила автоматизации по отдельному внешнему событию.
/// Сущность используется для аудита, аналитики и предотвращения повторного выполнения одного и того же сценария.
/// </summary>
public class AutomationExecutionLog : BaseEntity<Guid>
{
    /// <summary>
    /// Создает новый журнал исполнения правила автоматизации.
    /// </summary>
    /// <param name="automationRuleId">Идентификатор правила.</param>
    /// <param name="externalTriggerEventId">Идемпотентный идентификатор внешнего события.</param>
    /// <param name="triggerExternalUserId">Идентификатор внешнего пользователя-инициатора.</param>
    /// <param name="triggerExternalUserName">Имя внешнего пользователя.</param>
    /// <param name="triggerContent">Контент события-триггера.</param>
    /// <param name="executedAtUtc">UTC-время обработки события.</param>
    /// <param name="outcome">Итог исполнения.</param>
    /// <param name="skipReason">Причина пропуска.</param>
    /// <param name="errorMessage">Техническая ошибка.</param>
    /// <param name="pendingDmQueueId">Связанная запись очереди pending DM.</param>
    /// <returns>Новая запись журнала.</returns>
    public static AutomationExecutionLog Create(
        Guid automationRuleId,
        string externalTriggerEventId,
        string triggerExternalUserId,
        string? triggerExternalUserName,
        string? triggerContent,
        DateTime executedAtUtc,
        AutomationExecutionOutcome outcome,
        string? skipReason,
        string? errorMessage,
        Guid? pendingDmQueueId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalTriggerEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerExternalUserId);

        return new AutomationExecutionLog
        {
            Id = Guid.NewGuid(),
            AutomationRuleId = automationRuleId,
            ExternalTriggerEventId = externalTriggerEventId.Trim(),
            TriggerExternalUserId = triggerExternalUserId.Trim(),
            TriggerExternalUserName = string.IsNullOrWhiteSpace(triggerExternalUserName) ? null : triggerExternalUserName.Trim(),
            TriggerContent = string.IsNullOrWhiteSpace(triggerContent) ? null : triggerContent.Trim(),
            ExecutedAt = executedAtUtc,
            Outcome = outcome,
            SkipReason = string.IsNullOrWhiteSpace(skipReason) ? null : skipReason.Trim(),
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim(),
            PendingDMQueueId = pendingDmQueueId
        };
    }

    /// <summary>
    /// Идентификатор правила автоматизации, для которого сохранен результат выполнения.
    /// Поле позволяет анализировать эффективность и историю конкретного business-сценария.
    /// </summary>
    public Guid AutomationRuleId { get; private set; }

    /// <summary>
    /// Идентификатор внешнего события, которое стало причиной срабатывания.
    /// Это значение используется как ключ идемпотентности и защищает от повторной обработки одного триггера.
    /// </summary>
    public string ExternalTriggerEventId { get; private set; } = default!;

    /// <summary>
    /// Идентификатор внешнего пользователя, инициировавшего триггер.
    /// Поле помогает отслеживать персональную историю реакций системы по отношению к одному человеку.
    /// </summary>
    public string TriggerExternalUserId { get; private set; } = default!;

    /// <summary>
    /// Username внешнего пользователя на момент срабатывания правила.
    /// Значение делает журнал понятнее для оператора и поддержки.
    /// </summary>
    public string? TriggerExternalUserName { get; private set; }

    /// <summary>
    /// Содержимое события, например текст комментария или сообщения, породившего реакцию.
    /// Поле помогает восстанавливать контекст без повторной загрузки первичных данных с платформы.
    /// </summary>
    public string? TriggerContent { get; private set; }

    /// <summary>
    /// Дата и время фактического выполнения сценария в UTC.
    /// Метка фиксирует момент, когда система приняла решение по конкретному триггеру.
    /// </summary>
    public DateTime ExecutedAt { get; private set; }

    /// <summary>
    /// Финальный результат исполнения правила по конкретному событию.
    /// Поле показывает, было ли действие выполнено, отложено, пропущено или завершилось ошибкой.
    /// </summary>
    public AutomationExecutionOutcome Outcome { get; private set; }

    /// <summary>
    /// Причина пропуска выполнения, если сценарий не был исполнен.
    /// Значение помогает отличать безопасный business-skip от технической ошибки.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>
    /// Текстовое описание ошибки, возникшей во время вызова внешней платформы или внутренней обработки.
    /// Поле используется для диагностики неуспешных срабатываний.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Ссылка на запись в очереди отложенных сообщений, если сценарий был переведен в ожидание.
    /// Значение помогает соединить историю срабатывания с последующей доставкой DM.
    /// </summary>
    public Guid? PendingDMQueueId { get; private set; }

    /// <summary>
    /// Правило автоматизации, для которого сохранен журнал выполнения.
    /// Навигация объединяет фактический результат с конфигурацией сценария.
    /// </summary>
    public AutomationRule AutomationRule { get; private set; } = default!;
}
