using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Подключенный внешний аккаунт на социальной платформе.
/// Сущность служит мостом между внутренней моделью AutoPost и конкретным каналом публикации, общения и аналитики.
/// </summary>
public class SocialAccount : BaseEntity<Guid>
{
    /// <summary>
    /// Creates a new connected social account aggregate.
    /// </summary>
    /// <param name="workspaceId">Workspace that owns the integration.</param>
    /// <param name="platform">External social platform.</param>
    /// <param name="externalAccountId">Platform-side account identifier.</param>
    /// <param name="accountDisplayName">Public display name of the connected account.</param>
    /// <param name="accountUsername">Optional public handle or username.</param>
    /// <param name="accountAvatarUrl">Optional public avatar URL.</param>
    /// <param name="encryptedAccessToken">Protected OAuth access token payload.</param>
    /// <param name="encryptedRefreshToken">Protected OAuth refresh token payload.</param>
    /// <param name="tokenExpiresAt">UTC access-token expiration timestamp.</param>
    /// <param name="grantedScopes">Granted OAuth scopes stored as CSV.</param>
    /// <param name="connectedAt">UTC timestamp when the integration was connected.</param>
    /// <param name="accountType">Optional provider-specific account type.</param>
    /// <param name="isPrivateAccount">Whether the remote account is private.</param>
    /// <param name="followersCount">Optional cached follower count.</param>
    /// <returns>A fully initialized <see cref="SocialAccount"/> aggregate.</returns>
    public static SocialAccount Connect(
        Guid workspaceId,
        Platform platform,
        string externalAccountId,
        string accountDisplayName,
        string? accountUsername,
        string? accountAvatarUrl,
        string encryptedAccessToken,
        string? encryptedRefreshToken,
        DateTime tokenExpiresAt,
        string grantedScopes,
        DateTime connectedAt,
        string? accountType = null,
        bool isPrivateAccount = false,
        long? followersCount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedAccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantedScopes);

        return new SocialAccount
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Platform = platform,
            ExternalAccountId = externalAccountId,
            AccountDisplayName = accountDisplayName,
            AccountUsername = accountUsername,
            AccountAvatarUrl = accountAvatarUrl,
            EncryptedAccessToken = encryptedAccessToken,
            EncryptedRefreshToken = encryptedRefreshToken,
            TokenExpiresAt = tokenExpiresAt,
            GrantedScopes = grantedScopes,
            Status = SocialAccountStatus.Active,
            AccountType = accountType,
            IsPrivateAccount = isPrivateAccount,
            FollowersCount = followersCount,
            FollowersCountUpdatedAt = followersCount.HasValue ? connectedAt : null,
            ConnectedAt = connectedAt
        };
    }

    /// <summary>
    /// Идентификатор рабочего пространства, которому принадлежит подключенный аккаунт.
    /// Благодаря этому полю социальный профиль изолируется внутри tenant-контекста команды.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Платформа, к которой относится данный внешний аккаунт.
    /// Значение определяет используемый API-контракт и допустимые сценарии публикации или коммуникации.
    /// </summary>
    public Platform Platform { get; private set; }

    /// <summary>
    /// Уникальный идентификатор аккаунта на стороне внешней платформы.
    /// Это поле позволяет однозначно сопоставить доменный объект с реальным каналом или профилем.
    /// </summary>
    public string ExternalAccountId { get; private set; } = default!;

    /// <summary>
    /// Отображаемое имя канала, страницы или профиля на платформе.
    /// Значение используется в интерфейсе выбора целей публикации и в списках интеграций.
    /// </summary>
    public string AccountDisplayName { get; private set; } = default!;

    /// <summary>
    /// Публичный username или handle аккаунта, если платформа предоставляет такой атрибут.
    /// Поле удобно для отображения в интерфейсе и в сценариях работы с упоминаниями.
    /// </summary>
    public string? AccountUsername { get; private set; }

    /// <summary>
    /// URL аватара внешнего аккаунта.
    /// Используется для визуального представления подключенного профиля без постоянных обращений к API платформы.
    /// </summary>
    public string? AccountAvatarUrl { get; private set; }

    /// <summary>
    /// Зашифрованный access-token, необходимый для обращения к API платформы.
    /// Доменная модель хранит защищенное значение, поскольку это чувствительный интеграционный секрет.
    /// </summary>
    public string EncryptedAccessToken { get; private set; } = default!;

    /// <summary>
    /// Зашифрованный refresh-token для обновления доступа, если соответствующая платформа его выдает.
    /// Значение может отсутствовать у платформ и тарифов API, где механизм обновления токена не предусмотрен.
    /// </summary>
    public string? EncryptedRefreshToken { get; private set; }

    /// <summary>
    /// Момент истечения срока действия access-токена в UTC.
    /// Это поле используется для принятия решения, когда требуется обновление доступа перед публикацией или синхронизацией.
    /// </summary>
    public DateTime TokenExpiresAt { get; private set; }

    /// <summary>
    /// Набор OAuth-scopes, выданных приложению в рамках авторизации.
    /// Содержимое помогает понимать, какие операции разрешены для данного аккаунта на стороне платформы.
    /// </summary>
    public string GrantedScopes { get; private set; } = default!;

    /// <summary>
    /// Текущий статус связи с внешним аккаунтом.
    /// Через это поле доменная модель отличает рабочие интеграции от тех, что требуют повторного подключения.
    /// </summary>
    public SocialAccountStatus Status { get; private set; }

    /// <summary>
    /// Тип аккаунта на платформе, если он влияет на доступные возможности API.
    /// Например, для Instagram от этого значения зависит доступность публикации или автоматизации direct messages.
    /// </summary>
    public string? AccountType { get; private set; }

    /// <summary>
    /// Признак приватности внешнего аккаунта или собеседника в контексте интеграции.
    /// Значение используется в логике отложенной отправки DM, когда прямой контакт зависит от подписки.
    /// </summary>
    public bool IsPrivateAccount { get; private set; }

    /// <summary>
    /// Последнее известное количество подписчиков.
    /// Поле хранится как кэш и помогает строить дашборды роста без обращения к API при каждом просмотре интерфейса.
    /// </summary>
    public long? FollowersCount { get; private set; }

    /// <summary>
    /// Время последнего обновления кэшированного счетчика подписчиков.
    /// Значение позволяет оценить свежесть показателя в аналитике и UI.
    /// </summary>
    public DateTime? FollowersCountUpdatedAt { get; private set; }

    /// <summary>
    /// Дата и время первоначального подключения аккаунта к платформе AutoPost.
    /// Поле фиксирует момент начала использования интеграции в конкретном workspace.
    /// </summary>
    public DateTime ConnectedAt { get; private set; }

    /// <summary>
    /// Дата и время отключения интеграции пользователем, если связь была разорвана.
    /// Пока аккаунт активен, значение остается пустым.
    /// </summary>
    public DateTime? DisconnectedAt { get; private set; }

    /// <summary>
    /// Рабочее пространство, к которому относится подключенный аккаунт.
    /// Навигация задает tenant-границу для всех публикаций и коммуникаций, связанных с профилем.
    /// </summary>
    public Workspace Workspace { get; private set; } = default!;

    /// <summary>
    /// Все целевые публикации, в которых данный аккаунт использовался как канал размещения контента.
    /// Коллекция дает историю задействования профиля в контент-плане команды.
    /// </summary>
    public IReadOnlyCollection<PostTarget> PostTargets { get; private set; } = new List<PostTarget>();

    /// <summary>
    /// Переписки unified inbox, связанные с данным внешним аккаунтом.
    /// Через эту навигацию объединяются входящие сообщения и комментарии, пришедшие в конкретный профиль.
    /// </summary>
    public IReadOnlyCollection<InboxConversation> Conversations { get; private set; } = new List<InboxConversation>();

    /// <summary>
    /// Правила автоматизации, настроенные для данного социального аккаунта.
    /// Коллекция отражает сценарии реакций, привязанные к конкретному внешнему каналу.
    /// </summary>
    public IReadOnlyCollection<AutomationRule> AutomationRules { get; private set; } = new List<AutomationRule>();

    /// <summary>
    /// Последний доступный снимок аналитики данного аккаунта.
    /// Навигация позволяет быстро получать актуальный агрегированный показатель роста профиля.
    /// </summary>
    public SocialAccountInsight? LatestInsight { get; private set; }

    /// <summary>
    /// Updates protected OAuth credentials after a successful authorization or refresh flow.
    /// </summary>
    /// <param name="encryptedAccessToken">Protected access token payload.</param>
    /// <param name="encryptedRefreshToken">Protected refresh token payload when available.</param>
    /// <param name="tokenExpiresAt">UTC expiration timestamp for the access token.</param>
    /// <param name="grantedScopes">Granted scope set stored as CSV.</param>
    public void UpdateCredentials(
        string encryptedAccessToken,
        string? encryptedRefreshToken,
        DateTime tokenExpiresAt,
        string grantedScopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedAccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantedScopes);

        EncryptedAccessToken = encryptedAccessToken;
        EncryptedRefreshToken = encryptedRefreshToken;
        TokenExpiresAt = tokenExpiresAt;
        GrantedScopes = grantedScopes;
        Status = SocialAccountStatus.Active;
        DisconnectedAt = null;
    }

    /// <summary>
    /// Refreshes cached public profile metadata for the connected account.
    /// </summary>
    /// <param name="accountDisplayName">Public display name returned by the provider.</param>
    /// <param name="accountUsername">Optional public handle or username.</param>
    /// <param name="accountAvatarUrl">Optional avatar URL.</param>
    /// <param name="accountType">Optional provider-specific account type.</param>
    /// <param name="isPrivateAccount">Whether the account is private.</param>
    /// <param name="followersCount">Optional cached follower count.</param>
    /// <param name="observedAtUtc">UTC timestamp when the metadata was observed.</param>
    public void RefreshProfile(
        string accountDisplayName,
        string? accountUsername,
        string? accountAvatarUrl,
        string? accountType,
        bool isPrivateAccount,
        long? followersCount,
        DateTime observedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountDisplayName);

        AccountDisplayName = accountDisplayName;
        AccountUsername = accountUsername;
        AccountAvatarUrl = accountAvatarUrl;
        AccountType = accountType;
        IsPrivateAccount = isPrivateAccount;
        FollowersCount = followersCount;
        FollowersCountUpdatedAt = followersCount.HasValue ? observedAtUtc : FollowersCountUpdatedAt;
    }

    /// <summary>
    /// Marks the integration as disconnected by the workspace user.
    /// </summary>
    /// <param name="disconnectedAtUtc">UTC timestamp of the disconnection event.</param>
    public void Disconnect(DateTime disconnectedAtUtc)
    {
        Status = SocialAccountStatus.Disconnected;
        DisconnectedAt = disconnectedAtUtc;
    }

    /// <summary>
    /// Marks the remote provider access as revoked or otherwise invalid.
    /// </summary>
    /// <param name="revokedAtUtc">UTC timestamp when revocation was detected.</param>
    public void Revoke(DateTime revokedAtUtc)
    {
        Status = SocialAccountStatus.Revoked;
        DisconnectedAt = revokedAtUtc;
    }

    /// <summary>
    /// Marks the integration access token as expired and in need of refresh.
    /// </summary>
    public void MarkTokenExpired()
    {
        Status = SocialAccountStatus.TokenExpired;
    }

    /// <summary>
    /// Restores the integration to an active state after a successful validation or refresh.
    /// </summary>
    public void MarkActive()
    {
        Status = SocialAccountStatus.Active;
    }

    /// <summary>
    /// Checks whether the access token should be treated as expired at the supplied UTC time.
    /// </summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns><c>true</c> when the access token is already expired or close to expiry.</returns>
    public bool IsTokenExpired(DateTime utcNow) => TokenExpiresAt <= utcNow;
}
