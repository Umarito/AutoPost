using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Условие, входящее в правило автоматизации.
/// Сущность описывает конкретную проверку, которую должна пройти входящая активность перед запуском действий.
/// </summary>
public class TriggerCondition : BaseEntity<Guid>
{
    /// <summary>
    /// Создает новое условие для правила автоматизации.
    /// </summary>
    /// <param name="automationRuleId">Идентификатор родительского правила.</param>
    /// <param name="type">Тип проверяемого атрибута.</param>
    /// <param name="conditionOperator">Оператор сравнения.</param>
    /// <param name="value">Эталонное значение сравнения.</param>
    /// <param name="isCaseSensitive">Флаг чувствительности к регистру.</param>
    /// <returns>Новая сущность условия.</returns>
    public static TriggerCondition Create(
        Guid automationRuleId,
        ConditionType type,
        ConditionOperator conditionOperator,
        string? value,
        bool isCaseSensitive)
    {
        return new TriggerCondition
        {
            Id = Guid.NewGuid(),
            AutomationRuleId = automationRuleId,
            Type = type,
            Operator = conditionOperator,
            Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim(),
            IsCaseSensitive = isCaseSensitive
        };
    }

    /// <summary>
    /// Идентификатор правила автоматизации, которому принадлежит условие.
    /// Поле закрепляет условие за конкретным automation-сценарием.
    /// </summary>
    public Guid AutomationRuleId { get; private set; }

    /// <summary>
    /// Тип проверяемого атрибута события.
    /// Значение указывает, что именно система должна анализировать: текст, подписку, публичность профиля и так далее.
    /// </summary>
    public ConditionType Type { get; private set; }

    /// <summary>
    /// Оператор, определяющий способ сравнения входного значения с ожидаемым.
    /// Поле задает логику проверки, например contains, equals или any.
    /// </summary>
    public ConditionOperator Operator { get; private set; }

    /// <summary>
    /// Эталонное значение для сравнения.
    /// В зависимости от оператора здесь может храниться ключевое слово, фраза или иной критерий фильтрации.
    /// </summary>
    public string? Value { get; private set; }

    /// <summary>
    /// Признак чувствительности к регистру при сравнении.
    /// Поле позволяет тонко управлять сценариями, где важна точная форма текста.
    /// </summary>
    public bool IsCaseSensitive { get; private set; }

    /// <summary>
    /// Правило автоматизации, которому принадлежит текущее условие.
    /// Навигация объединяет отдельную проверку с полным automation-сценарием.
    /// </summary>
    public AutomationRule AutomationRule { get; private set; } = default!;
}
