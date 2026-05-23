using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Действие, которое выполняется после успешного срабатывания правила автоматизации.
/// Сущность описывает конкретный бизнес-эффект сценария: сообщение, лайк, назначение диалога и другие реакции.
/// </summary>
public class AutomationAction : BaseEntity<Guid>
{
    /// <summary>
    /// Создает новое действие правила автоматизации.
    /// </summary>
    /// <param name="automationRuleId">Идентификатор родительского правила.</param>
    /// <param name="type">Тип выполняемого действия.</param>
    /// <param name="executionOrder">Порядок выполнения в сценарии.</param>
    /// <param name="delaySeconds">Задержка перед выполнением.</param>
    /// <param name="messageTemplate">Шаблон текста действия.</param>
    /// <param name="linkUrl">Опциональная ссылка, подставляемая в действие.</param>
    /// <returns>Новая сущность действия.</returns>
    public static AutomationAction Create(
        Guid automationRuleId,
        ActionType type,
        int executionOrder,
        int delaySeconds,
        string? messageTemplate,
        string? linkUrl)
    {
        return new AutomationAction
        {
            Id = Guid.NewGuid(),
            AutomationRuleId = automationRuleId,
            Type = type,
            ExecutionOrder = executionOrder,
            DelaySeconds = Math.Max(0, delaySeconds),
            MessageTemplate = string.IsNullOrWhiteSpace(messageTemplate) ? null : messageTemplate.Trim(),
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim()
        };
    }

    /// <summary>
    /// Идентификатор правила автоматизации, которому принадлежит действие.
    /// Поле связывает реакцию с ее исходным business-сценарием.
    /// </summary>
    public Guid AutomationRuleId { get; private set; }

    /// <summary>
    /// Тип выполняемого действия.
    /// От него зависит, будет ли система отправлять DM, ставить лайк, отвечать публично или менять состояние inbox.
    /// </summary>
    public ActionType Type { get; private set; }

    /// <summary>
    /// Порядок выполнения действия внутри набора реакций одного правила.
    /// Значение позволяет строить последовательные automation-сценарии из нескольких шагов.
    /// </summary>
    public int ExecutionOrder { get; private set; }

    /// <summary>
    /// Задержка перед выполнением действия в секундах.
    /// Поле используется для более естественного поведения автоматизации и для отложенного исполнения реакций.
    /// </summary>
    public int DelaySeconds { get; private set; }

    /// <summary>
    /// Шаблон текста, который должен быть использован для ответа или сообщения.
    /// Поле поддерживает параметры сценария и подстановки переменных, описанные в TRD.
    /// </summary>
    public string? MessageTemplate { get; private set; }

    /// <summary>
    /// Дополнительная ссылка, используемая в действии, например в шаблоне DM.
    /// Значение помогает инкапсулировать в доменной модели конкретный URL, передаваемый аудитории при срабатывании правила.
    /// </summary>
    public string? LinkUrl { get; private set; }

    /// <summary>
    /// Правило автоматизации, которому принадлежит действие.
    /// Навигация связывает конкретную реакцию с общей бизнес-логикой сценария.
    /// </summary>
    public AutomationRule AutomationRule { get; private set; } = default!;
}
