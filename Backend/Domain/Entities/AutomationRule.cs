using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Правило автоматизации реакции на внешние события.
/// Сущность объединяет триггер, условия и набор действий, которые AutoPost должен выполнить при наступлении события.
/// </summary>
public class AutomationRule : AuditableEntity<Guid>
{
    /// <summary>
    /// Создает новое правило автоматизации с полным набором условий и действий.
    /// </summary>
    /// <param name="workspaceId">Идентификатор рабочего пространства.</param>
    /// <param name="socialAccountId">Идентификатор социального аккаунта.</param>
    /// <param name="name">Название правила.</param>
    /// <param name="description">Описание правила.</param>
    /// <param name="triggerType">Тип триггера.</param>
    /// <param name="targetExternalPostId">Опциональный внешний идентификатор поста.</param>
    /// <param name="maxActionsPerUser">Лимит срабатываний на одного внешнего пользователя.</param>
    /// <param name="dailyExecutionLimit">Опциональный дневной лимит.</param>
    /// <param name="conditions">Условия правила.</param>
    /// <param name="actions">Действия правила.</param>
    /// <param name="createdAtUtc">UTC-время создания правила.</param>
    /// <returns>Новый aggregate root правила автоматизации.</returns>
    public static AutomationRule Create(
        Guid workspaceId,
        Guid socialAccountId,
        string name,
        string? description,
        AutomationTriggerType triggerType,
        string? targetExternalPostId,
        int maxActionsPerUser,
        int? dailyExecutionLimit,
        IReadOnlyCollection<TriggerCondition> conditions,
        IReadOnlyCollection<AutomationAction> actions,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(actions);

        return new AutomationRule
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            SocialAccountId = socialAccountId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsEnabled = true,
            TriggerType = triggerType,
            TargetExternalPostId = string.IsNullOrWhiteSpace(targetExternalPostId) ? null : targetExternalPostId.Trim(),
            MaxActionsPerUser = Math.Max(1, maxActionsPerUser),
            DailyExecutionLimit = dailyExecutionLimit,
            TodayExecutionCount = 0,
            Conditions = conditions.ToArray(),
            Actions = actions.OrderBy(action => action.ExecutionOrder).ToArray(),
            CreatedAt = createdAtUtc,
            UpdatedAt = createdAtUtc
        };
    }

    /// <summary>
    /// Идентификатор рабочего пространства, в рамках которого настроено правило.
    /// Поле гарантирует, что автоматизация работает только в пределах конкретной команды.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Идентификатор социального аккаунта, к которому привязано правило.
    /// Автоматизация реагирует только на события, возникающие в указанном внешнем канале.
    /// </summary>
    public Guid SocialAccountId { get; private set; }

    /// <summary>
    /// Название правила, используемое для навигации и понимания сценария в интерфейсе команды.
    /// Поле помогает быстро распознавать назначение automation-сценария.
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Подробное описание бизнес-смысла правила.
    /// Значение полезно для командной поддержки и понимания причин существования автоматизации.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Признак того, что правило включено и может срабатывать.
    /// Выключение позволяет временно остановить automation без удаления ее структуры и истории.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Тип события, на которое реагирует правило.
    /// Поле определяет начальную точку запуска сценария автоматизации.
    /// </summary>
    public AutomationTriggerType TriggerType { get; private set; }

    /// <summary>
    /// Идентификатор конкретного внешнего поста, к которому привязано правило, если сценарий ограничен одним материалом.
    /// Отсутствие значения означает, что автоматизация применяется ко всему аккаунту.
    /// </summary>
    public string? TargetExternalPostId { get; private set; }

    /// <summary>
    /// Максимальное количество срабатываний правила для одного внешнего пользователя.
    /// Поле защищает команду и платформенный аккаунт от избыточных повторных коммуникаций с одним и тем же человеком.
    /// </summary>
    public int MaxActionsPerUser { get; private set; }

    /// <summary>
    /// Глобальный лимит срабатываний правила в сутки.
    /// Значение ограничивает массовые автоматические реакции и служит защитой от спама.
    /// </summary>
    public int? DailyExecutionLimit { get; private set; }

    /// <summary>
    /// Счетчик срабатываний правила за текущий день.
    /// Поле позволяет быстро оценить, приближается ли правило к заданному суточному лимиту.
    /// </summary>
    public int TodayExecutionCount { get; private set; }

    /// <summary>
    /// Рабочее пространство, которому принадлежит правило.
    /// Навигация связывает automation-сценарий с tenant-контекстом команды.
    /// </summary>
    public Workspace Workspace { get; private set; } = default!;

    /// <summary>
    /// Социальный аккаунт, для которого действует правило автоматизации.
    /// Навигация задает внешний канал, где система наблюдает и исполняет сценарий.
    /// </summary>
    public SocialAccount SocialAccount { get; private set; } = default!;

    /// <summary>
    /// Условия, которые должны выполниться для запуска действий правила.
    /// Коллекция описывает бизнес-фильтры поверх самого факта наступления триггерного события.
    /// </summary>
    public IReadOnlyCollection<TriggerCondition> Conditions { get; private set; } = new List<TriggerCondition>();

    /// <summary>
    /// Действия, которые система должна выполнить после успешного прохождения условий.
    /// Порядок внутри коллекции важен для сценариев с несколькими последовательными реакциями.
    /// </summary>
    public IReadOnlyCollection<AutomationAction> Actions { get; private set; } = new List<AutomationAction>();

    /// <summary>
    /// История фактических срабатываний правила.
    /// Навигация используется для аудита, аналитики и защиты от повторного исполнения.
    /// </summary>
    public IReadOnlyCollection<AutomationExecutionLog> ExecutionLogs { get; private set; } = new List<AutomationExecutionLog>();

    /// <summary>
    /// Обновляет определение правила без потери его идентичности и истории исполнения.
    /// </summary>
    /// <param name="name">Новое название.</param>
    /// <param name="description">Новое описание.</param>
    /// <param name="socialAccountId">Новый идентификатор аккаунта.</param>
    /// <param name="triggerType">Новый тип триггера.</param>
    /// <param name="targetExternalPostId">Новый внешний идентификатор поста.</param>
    /// <param name="maxActionsPerUser">Новый лимит на пользователя.</param>
    /// <param name="dailyExecutionLimit">Новый дневной лимит.</param>
    /// <param name="conditions">Полностью обновленный набор условий.</param>
    /// <param name="actions">Полностью обновленный набор действий.</param>
    /// <param name="updatedAtUtc">UTC-время изменения.</param>
    public void UpdateDefinition(
        string name,
        string? description,
        Guid socialAccountId,
        AutomationTriggerType triggerType,
        string? targetExternalPostId,
        int maxActionsPerUser,
        int? dailyExecutionLimit,
        IReadOnlyCollection<TriggerCondition> conditions,
        IReadOnlyCollection<AutomationAction> actions,
        DateTime updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(actions);

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SocialAccountId = socialAccountId;
        TriggerType = triggerType;
        TargetExternalPostId = string.IsNullOrWhiteSpace(targetExternalPostId) ? null : targetExternalPostId.Trim();
        MaxActionsPerUser = Math.Max(1, maxActionsPerUser);
        DailyExecutionLimit = dailyExecutionLimit;
        Conditions = conditions.ToArray();
        Actions = actions.OrderBy(action => action.ExecutionOrder).ToArray();
        UpdatedAt = updatedAtUtc;
    }

    /// <summary>
    /// Включает или выключает правило автоматизации.
    /// </summary>
    /// <param name="isEnabled">Новое состояние активности.</param>
    /// <param name="updatedAtUtc">UTC-время изменения.</param>
    public void SetEnabled(bool isEnabled, DateTime updatedAtUtc)
    {
        IsEnabled = isEnabled;
        UpdatedAt = updatedAtUtc;
    }

    /// <summary>
    /// Проверяет, исчерпан ли дневной лимит правила на момент оценки триггера.
    /// </summary>
    /// <returns><c>true</c>, если лимит задан и уже достигнут; иначе <c>false</c>.</returns>
    public bool HasReachedDailyLimit()
        => DailyExecutionLimit.HasValue && TodayExecutionCount >= DailyExecutionLimit.Value;

    /// <summary>
    /// Увеличивает дневной счетчик успешных или поставленных в очередь исполнений.
    /// </summary>
    /// <param name="updatedAtUtc">UTC-время изменения.</param>
    public void IncrementTodayExecutionCount(DateTime updatedAtUtc)
    {
        TodayExecutionCount++;
        UpdatedAt = updatedAtUtc;
    }

    /// <summary>
    /// Сбрасывает дневной счетчик срабатываний.
    /// </summary>
    /// <param name="updatedAtUtc">UTC-время изменения.</param>
    public void ResetDailyExecutionCount(DateTime updatedAtUtc)
    {
        TodayExecutionCount = 0;
        UpdatedAt = updatedAtUtc;
    }
}
