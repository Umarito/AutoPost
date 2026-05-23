namespace Domain.Enums;

/// <summary>
/// Причина, по которой личное сообщение не было отправлено немедленно
/// и было помещено в очередь отложенной доставки.
/// </summary>
public enum PendingReason
{
    /// <summary>
    /// Целевой пользователь имеет приватный аккаунт и пока не создал условий для получения сообщения.
    /// </summary>
    TargetAccountIsPrivate,

    /// <summary>
    /// Внешняя платформа временно ограничила частоту обращений к API.
    /// </summary>
    ApiRateLimitReached,

    /// <summary>
    /// Внешний пользователь отключил возможность получения direct messages.
    /// </summary>
    DMsDisabledByUser
}
