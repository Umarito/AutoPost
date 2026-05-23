// ============================================================================
//  Auto-Posting & Unified Inbox Platform
//  Domain Layer — Все сущности (Entities + Value Objects)
//
//  Структура файла:
//    БЛОК I   — Пользователи и Доступ         (1–4)
//    БЛОК II  — Социальные Аккаунты           (5–6)
//    БЛОК III — Видео и Контент               (7–8)
//    БЛОК IV  — Публикации                    (9–15)
//    БЛОК V   — Unified Inbox                 (16–18)
//    БЛОК VI  — Автоматизация (DM Automation) (19–23)
//    БЛОК VII — Инфраструктурные сущности     (24–25)
//    ENUMS    — Все перечисления
// ============================================================================

using System;
using System.Collections.Generic;

namespace Platform.Domain.Entities;

// ════════════════════════════════════════════════════════════════════════════
//  БЛОК I — Пользователи и Доступ
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Aggregate Root всей системы.
/// Изолированное пространство одной компании или команды.
/// Все данные (аккаунты, посты, правила автоматизации) принадлежат Workspace,
/// а не напрямую пользователю. Фундамент для RBAC и multi-tenancy.
/// </summary>
public class Workspace
{
    public Guid Id { get; private set; }

    /// <summary>Название организации или команды.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Уникальный slug для URL.
    /// Пример: app.yourplatform.com/acme-corp
    /// </summary>
    public string Slug { get; private set; } = default!;

    /// <summary>URL логотипа, хранится в Blob Storage.</summary>
    public string? LogoUrl { get; private set; }

    /// <summary>Тарифный план: Free, Pro, Business. Влияет на лимиты.</summary>
    public SubscriptionPlan Plan { get; private set; }

    /// <summary>Дата окончания подписки. null = Free навсегда.</summary>
    public DateTime? PlanExpiresAt { get; private set; }

    /// <summary>Максимальное число подключённых SocialAccount на текущем плане.</summary>
    public int MaxSocialAccounts { get; private set; }

    /// <summary>Максимальное число участников команды на текущем плане.</summary>
    public int MaxTeamMembers { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>Soft-деактивация Workspace без физического удаления данных.</summary>
    public bool IsActive { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public IReadOnlyCollection<WorkspaceMember> Members { get; private set; }
        = new List<WorkspaceMember>();

    public IReadOnlyCollection<SocialAccount> SocialAccounts { get; private set; }
        = new List<SocialAccount>();

    public IReadOnlyCollection<Post> Posts { get; private set; }
        = new List<Post>();

    public IReadOnlyCollection<AutomationRule> AutomationRules { get; private set; }
        = new List<AutomationRule>();
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Aggregate Root. Учётная запись конкретного человека.
/// Пользователь может быть членом нескольких Workspace с разными ролями
/// (например, Owner в своей компании и Editor в клиентской).
/// </summary>
public class ApplicationUser
{
    public Guid Id { get; private set; }

    /// <summary>Email — основной логин для входа.</summary>
    public string Email { get; private set; } = default!;

    /// <summary>Хэш пароля. Никогда не хранится в открытом виде.</summary>
    public string PasswordHash { get; private set; } = default!;

    /// <summary>Отображаемое имя в интерфейсе.</summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>URL аватара пользователя.</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>
    /// Предпочитаемый часовой пояс.
    /// Хранится как IANA timezone string: "Asia/Dushanbe", "Europe/Moscow".
    /// Используется только для отображения — все даты внутри системы в UTC.
    /// </summary>
    public string TimeZoneId { get; private set; } = "UTC";

    /// <summary>Язык интерфейса: "ru", "en".</summary>
    public string Locale { get; private set; } = "en";

    public DateTime RegisteredAt { get; private set; }

    /// <summary>Дата последнего входа. Полезно для аудита и security-политик.</summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// Soft delete: деактивированный пользователь не удаляется из БД,
    /// но не может войти и выполнять действия.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Подтверждён ли email.
    /// Неподтверждённый пользователь не может публиковать посты.
    /// </summary>
    public bool IsEmailConfirmed { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────

    /// <summary>Членство в Workspace'ах (один пользователь — много Workspace).</summary>
    public IReadOnlyCollection<WorkspaceMember> WorkspaceMemberships { get; private set; }
        = new List<WorkspaceMember>();

    /// <summary>Активные сессии. Используется для "выйти со всех устройств".</summary>
    public IReadOnlyCollection<RefreshToken> RefreshTokens { get; private set; }
        = new List<RefreshToken>();

    /// <summary>Настройки уведомлений пользователя.</summary>
    public IReadOnlyCollection<NotificationPreference> NotificationPreferences { get; private set; }
        = new List<NotificationPreference>();
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Связующая сущность между ApplicationUser и Workspace.
/// Определяет роль пользователя в конкретном Workspace. Основа RBAC.
///
/// Роли:
///   Owner  — полный доступ, включая billing и удаление Workspace
///   Admin  — управление командой и подключёнными аккаунтами
///   Editor — создание и редактирование постов
///   Viewer — только просмотр (аналитика, Inbox — read-only)
/// </summary>
public class WorkspaceMember
{
    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Роль пользователя в этом Workspace.</summary>
    public WorkspaceRole Role { get; private set; }

    /// <summary>Статус участника: Invited → Active → Suspended.</summary>
    public MemberStatus Status { get; private set; }

    /// <summary>Email, на который было отправлено приглашение.</summary>
    public string InvitedEmail { get; private set; } = default!;

    /// <summary>Кто пригласил этого участника (для аудита). null = системное добавление.</summary>
    public Guid? InvitedByUserId { get; private set; }

    public DateTime InvitedAt { get; private set; }

    /// <summary>Дата принятия приглашения. null если ещё не принято.</summary>
    public DateTime? JoinedAt { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Workspace Workspace { get; private set; } = default!;
    public ApplicationUser User { get; private set; } = default!;

    /// <summary>Пользователь, отправивший приглашение.</summary>
    public ApplicationUser? InvitedBy { get; private set; }
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// JWT Refresh Token для конкретной пользовательской сессии.
/// Позволяет инвалидировать отдельные сессии без смены пароля.
/// Реализует стратегию ротации: после использования токен помечается IsUsed
/// и выдаётся новый. Хранится как SHA-256 хэш (не plaintext).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 хэш токена. Plaintext никогда не хранится в БД.</summary>
    public string TokenHash { get; private set; } = default!;

    /// <summary>
    /// Устройство или браузер пользователя.
    /// Отображается в разделе "Активные сессии": "Chrome / Windows 11".
    /// </summary>
    public string? DeviceInfo { get; private set; }

    /// <summary>IP-адрес при выдаче токена. Для security-аудита.</summary>
    public string? IpAddress { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Токен уже был использован для получения нового.
    /// После использования токен инвалидируется — ротация.
    /// </summary>
    public bool IsUsed { get; private set; }

    /// <summary>Токен отозван вручную (logout / logout all devices).</summary>
    public bool IsRevoked { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public ApplicationUser User { get; private set; } = default!;
}

// ════════════════════════════════════════════════════════════════════════════
//  БЛОК II — Социальные Аккаунты
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Aggregate Root. Подключённый внешний аккаунт на социальной платформе
/// (YouTube-канал, Instagram-страница, TikTok-аккаунт и т.д.).
///
/// Это мост между системой и внешними платформами:
///   - через него публикуются посты
///   - через него получаются входящие сообщения в Inbox
///   - к нему привязываются правила автоматизации
///
/// Один пользователь может иметь несколько аккаунтов на одной платформе
/// (например, два YouTube-канала в одном Workspace).
/// </summary>
public class SocialAccount
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }

    /// <summary>Платформа: YouTube, Instagram, TikTok, Facebook, Twitter, Telegram.</summary>
    public Platform Platform { get; private set; }

    /// <summary>
    /// Уникальный ID аккаунта на платформе.
    /// Пример: "UC_x5XG1OV2P6uZZ5FSM9Ttw" для YouTube-канала.
    /// </summary>
    public string ExternalAccountId { get; private set; } = default!;

    /// <summary>Имя канала / страницы как оно отображается на платформе.</summary>
    public string AccountDisplayName { get; private set; } = default!;

    /// <summary>Username (если платформа поддерживает): @handle.</summary>
    public string? AccountUsername { get; private set; }

    /// <summary>
    /// URL аватара аккаунта.
    /// Кэшируем, чтобы не дёргать API при каждом отображении в UI.
    /// </summary>
    public string? AccountAvatarUrl { get; private set; }

    // ── OAuth токены ─────────────────────────────────────────────────────────
    // ВАЖНО: оба токена хранятся ЗАШИФРОВАННЫМИ через IDataProtectionProvider.
    // Plaintext токены НИКОГДА не попадают в БД, логи и API-ответы.

    /// <summary>Зашифрованный OAuth Access Token.</summary>
    public string EncryptedAccessToken { get; private set; } = default!;

    /// <summary>
    /// Зашифрованный OAuth Refresh Token.
    /// null для платформ, не поддерживающих refresh (например, Twitter v2 Basic).
    /// </summary>
    public string? EncryptedRefreshToken { get; private set; }

    /// <summary>Время истечения Access Token в UTC.</summary>
    public DateTime TokenExpiresAt { get; private set; }

    /// <summary>
    /// OAuth scopes, выданные при авторизации.
    /// Пример: "instagram_basic,instagram_content_publish,pages_messaging".
    /// </summary>
    public string GrantedScopes { get; private set; } = default!;

    /// <summary>Текущее состояние подключения аккаунта.</summary>
    public SocialAccountStatus Status { get; private set; }

    /// <summary>
    /// Тип аккаунта Instagram: Personal, Creator, Business.
    /// Влияет на доступные API-методы (Business требуется для DM Automation).
    /// </summary>
    public string? AccountType { get; private set; }

    /// <summary>
    /// Публичный или приватный аккаунт внешнего пользователя.
    /// Критично для логики PendingDMQueue:
    ///   приватный → DM можно отправить только после подписки.
    /// </summary>
    public bool IsPrivateAccount { get; private set; }

    /// <summary>Число подписчиков — кэш, обновляется по расписанию (раз в день).</summary>
    public long? FollowersCount { get; private set; }

    /// <summary>Время последнего обновления кэша подписчиков.</summary>
    public DateTime? FollowersCountUpdatedAt { get; private set; }

    public DateTime ConnectedAt { get; private set; }

    /// <summary>Дата отключения аккаунта пользователем.</summary>
    public DateTime? DisconnectedAt { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Workspace Workspace { get; private set; } = default!;

    /// <summary>Все PostTarget, использовавшие этот аккаунт как цель публикации.</summary>
    public IReadOnlyCollection<PostTarget> PostTargets { get; private set; }
        = new List<PostTarget>();

    /// <summary>Все диалоги Inbox, связанные с этим аккаунтом.</summary>
    public IReadOnlyCollection<InboxConversation> Conversations { get; private set; }
        = new List<InboxConversation>();

    /// <summary>Правила автоматизации, привязанные к этому аккаунту.</summary>
    public IReadOnlyCollection<AutomationRule> AutomationRules { get; private set; }
        = new List<AutomationRule>();

    /// <summary>Последний снимок аналитики аккаунта.</summary>
    public SocialAccountInsight? LatestInsight { get; private set; }
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Снимок аналитики аккаунта на определённый момент времени.
/// Хранится как временной ряд (time series) — одна запись в день/неделю.
/// Используется для построения графиков роста в дашборде.
/// </summary>
public class SocialAccountInsight
{
    public Guid Id { get; private set; }
    public Guid SocialAccountId { get; private set; }

    /// <summary>Момент снятия снимка (UTC). Обычно раз в сутки.</summary>
    public DateTime RecordedAt { get; private set; }

    public long FollowersCount { get; private set; }
    public long FollowingCount { get; private set; }
    public long TotalPostsCount { get; private set; }

    /// <summary>Суммарный охват профиля за период (если платформа предоставляет).</summary>
    public long? ProfileReach { get; private set; }

    /// <summary>Суммарные показы профиля за период.</summary>
    public long? ProfileImpressions { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public SocialAccount SocialAccount { get; private set; } = default!;
}

// ════════════════════════════════════════════════════════════════════════════
//  БЛОК III — Видео и Контент
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Aggregate Root. Загруженный медиафайл.
/// Независим от публикаций: одно видео может использоваться
/// в нескольких постах на разных платформах.
///
/// Жизненный цикл: Uploading → Processing → Ready → (Deleted).
/// Физическое удаление из Storage выполняется отдельным Hangfire job
/// после того как DeletedAt заполнен.
/// </summary>
public class Video
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }

    /// <summary>Кто загрузил файл (для аудита).</summary>
    public Guid UploadedByUserId { get; private set; }

    /// <summary>
    /// Ключ в Blob Storage / S3 (не полный URL — URL генерируется динамически через SAS/pre-signed URL).
    /// Пример: "workspaces/ws-abc123/videos/vid-def456.mp4"
    /// </summary>
    public string StorageKey { get; private set; } = default!;

    /// <summary>CDN URL для стриминга и превью в UI.</summary>
    public string? CdnUrl { get; private set; }

    /// <summary>Оригинальное имя файла при загрузке.</summary>
    public string OriginalFileName { get; private set; } = default!;

    /// <summary>MIME-тип: "video/mp4", "video/quicktime", "video/x-msvideo".</summary>
    public string ContentType { get; private set; } = default!;

    /// <summary>Размер файла в байтах.</summary>
    public long FileSizeBytes { get; private set; }

    /// <summary>
    /// URL превью-кадра (thumbnail).
    /// Генерируется автоматически через FFprobe или загружается вручную пользователем.
    /// </summary>
    public string? ThumbnailUrl { get; private set; }

    /// <summary>Статус обработки файла.</summary>
    public VideoStatus Status { get; private set; }

    /// <summary>Текст ошибки при обработке (если Status == Failed).</summary>
    public string? ProcessingError { get; private set; }

    public DateTime UploadedAt { get; private set; }

    /// <summary>
    /// Soft delete: заполняется при "удалении" пользователем.
    /// Физическое удаление из Storage — отдельный scheduled Hangfire job.
    /// GlobalQueryFilter в EF Core: .HasQueryFilter(v => v.DeletedAt == null).
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Workspace Workspace { get; private set; } = default!;
    public ApplicationUser UploadedBy { get; private set; } = default!;

    /// <summary>Технические характеристики, извлечённые после загрузки.</summary>
    public VideoMetadata? Metadata { get; private set; }

    /// <summary>Посты, использующие это видео.</summary>
    public IReadOnlyCollection<Post> Posts { get; private set; }
        = new List<Post>();
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Value Object / Owned Entity (EF Core OwnsOne).
/// Технические характеристики видеофайла, извлечённые после загрузки
/// с помощью FFprobe или аналога.
///
/// Используются для:
///   - валидации совместимости с платформами перед публикацией
///   - отображения информации о файле в UI
///   - автоматической генерации превью
/// </summary>
public class VideoMetadata
{
    /// <summary>
    /// Длительность видео в секундах.
    /// Лимиты платформ: TikTok ≤ 600 сек, Instagram Reels ≤ 90 сек, YouTube ≤ 12 часов.
    /// </summary>
    public int DurationSeconds { get; private set; }

    /// <summary>Ширина видео в пикселях.</summary>
    public int Width { get; private set; }

    /// <summary>Высота видео в пикселях.</summary>
    public int Height { get; private set; }

    /// <summary>Соотношение сторон: "16:9", "9:16", "1:1", "4:5".</summary>
    public string AspectRatio { get; private set; } = default!;

    /// <summary>Частота кадров в секунду.</summary>
    public double FrameRate { get; private set; }

    /// <summary>Видеокодек: "h264", "h265", "vp9". MP4/H.264 — универсальный вариант.</summary>
    public string VideoCodec { get; private set; } = default!;

    /// <summary>Аудиокодек: "aac", "mp3", "opus". null если аудиодорожки нет.</summary>
    public string? AudioCodec { get; private set; }

    /// <summary>Битрейт видео в bits/second.</summary>
    public long VideoBitrate { get; private set; }

    /// <summary>Наличие аудиодорожки. Некоторые платформы требуют аудио.</summary>
    public bool HasAudio { get; private set; }
}

// ════════════════════════════════════════════════════════════════════════════
//  БЛОК IV — Публикации
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Aggregate Root ⭐ — центральная сущность домена.
/// Представляет намерение опубликовать контент на одну или несколько платформ.
///
/// Жизненный цикл:
///   Draft → Scheduled → Publishing → Published / PartiallyFailed / Failed
///   Draft → Cancelled (отменён до публикации)
///
/// Post является корнем агрегата и владеет PostTarget'ами.
/// Изменять PostTarget можно только через методы Post.
///
/// Инвариант домена: нельзя запланировать Post в прошлом.
/// Эта проверка выполняется в методе Post.Schedule(), а не в сервисах.
/// </summary>
public class Post
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }

    /// <summary>Кто создал пост (для аудита и фильтрации "мои посты").</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>
    /// Видео для публикации.
    /// null — зарезервировано для будущего расширения на текстовые посты.
    /// </summary>
    public Guid? VideoId { get; private set; }

    /// <summary>
    /// Контент поста: заголовок, описание, теги, видимость.
    /// Owned Entity (Value Object) в EF Core — хранится в той же таблице.
    /// </summary>
    public PostContent Content { get; private set; } = default!;

    /// <summary>
    /// Расписание публикации.
    /// Owned Entity (Value Object) — хранит ScheduledAt в UTC и IANA timezone пользователя.
    /// </summary>
    public Schedule Schedule { get; private set; } = default!;

    /// <summary>Текущий статус жизненного цикла поста.</summary>
    public PostStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Фактическое время завершения публикации (UTC).
    /// Может отличаться от ScheduledAt из-за задержек Hangfire или retry.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Workspace Workspace { get; private set; } = default!;
    public ApplicationUser CreatedBy { get; private set; } = default!;
    public Video? Video { get; private set; }

    /// <summary>
    /// Цели публикации (один пост → много платформ).
    /// Каждая цель отслеживается независимо.
    /// </summary>
    public IReadOnlyCollection<PostTarget> Targets { get; private set; }
        = new List<PostTarget>();
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Value Object / Owned Entity (EF Core OwnsOne).
/// Весь текстовый контент поста.
/// Отделён от Post, чтобы чётко разграничить идентификацию и описание.
/// </summary>
public class PostContent
{
    /// <summary>
    /// Заголовок публикации.
    /// Обязателен для YouTube, опционален для Instagram и TikTok.
    /// </summary>
    public string? Title { get; private set; }

    /// <summary>
    /// Описание / caption публикации.
    /// Для Instagram — это основной текст поста.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Хэштеги без символа #: ["marketing", "tutorial"].
    /// EF Core хранит как JSON-колонку.
    /// </summary>
    public IReadOnlyList<string> Tags { get; private set; } = new List<string>();

    /// <summary>Видимость: Public, Unlisted (только по ссылке), Private.</summary>
    public Visibility Visibility { get; private set; }

    /// <summary>
    /// URL кастомного thumbnail.
    /// Если задан — переопределяет автогенерированный из Video.ThumbnailUrl.
    /// </summary>
    public string? CustomThumbnailUrl { get; private set; }

    /// <summary>
    /// Платформо-специфичные настройки в JSON-формате.
    /// Пример для YouTube: { "madeForKids": false, "categoryId": "22", "license": "youtube" }
    /// Пример для TikTok: { "allowDuet": true, "allowStitch": false }
    /// </summary>
    public string? PlatformSettingsJson { get; private set; }
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Value Object / Owned Entity (EF Core OwnsOne).
/// Временна́я составляющая публикации — когда и в каком часовом поясе запланирован пост.
/// </summary>
public class Schedule
{
    /// <summary>
    /// Время публикации в UTC. Всегда UTC — без исключений.
    /// Конвертация в локальное время выполняется только на фронтенде
    /// с использованием TimeZoneId пользователя.
    /// </summary>
    public DateTime ScheduledAt { get; private set; }

    /// <summary>
    /// IANA timezone пользователя на момент создания поста.
    /// Хранится для корректного отображения в UI.
    /// Примеры: "Asia/Dushanbe", "Europe/Moscow", "America/New_York".
    /// </summary>
    public string TimeZoneId { get; private set; } = "UTC";

    /// <summary>
    /// ID job'а в Hangfire.
    /// Используется для отмены или перепланировки при редактировании поста.
    /// </summary>
    public string? SchedulerJobId { get; private set; }
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Конкретная цель публикации — пост на определённый аккаунт в определённой платформе.
/// Один Post имеет несколько PostTarget, каждый отслеживается независимо.
///
/// Пример: Post на YouTube + Instagram создаёт два PostTarget.
/// Если YouTube прошёл успешно, а Instagram упал — PostTarget.Status отражает это раздельно.
/// </summary>
public class PostTarget
{
    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public Guid SocialAccountId { get; private set; }

    /// <summary>
    /// Денормализованное поле для быстрых запросов без JOIN.
    /// Позволяет фильтровать "все посты на Instagram" без загрузки SocialAccount.
    /// </summary>
    public Platform Platform { get; private set; }

    /// <summary>Текущий статус этой конкретной цели.</summary>
    public TargetStatus Status { get; private set; }

    /// <summary>
    /// Результат публикации.
    /// null — пока не опубликовано (Status == Pending или Publishing).
    /// Заполняется после успешной или неуспешной публикации.
    /// </summary>
    public PostTargetResult? Result { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Post Post { get; private set; } = default!;
    public SocialAccount SocialAccount { get; private set; } = default!;

    /// <summary>История попыток публикации (для дебаггинга).</summary>
    public IReadOnlyCollection<PublishingJob> PublishingJobs { get; private set; }
        = new List<PublishingJob>();
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Value Object / Owned Entity (EF Core OwnsOne).
/// Итог попытки публикации на конкретную платформу.
/// Заполняется после завершения PublishingJob (успешного или нет).
/// </summary>
public class PostTargetResult
{
    /// <summary>
    /// ID созданного поста на платформе.
    /// Используется для последующего получения аналитики через API.
    /// Пример: "UCxxxxxxx" для YouTube, "17841400008460056" для Instagram.
    /// </summary>
    public string? ExternalPostId { get; private set; }

    /// <summary>Прямая ссылка на опубликованный пост.</summary>
    public string? ExternalPostUrl { get; private set; }

    /// <summary>Фактическое время публикации по данным платформы (UTC).</summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>Код ошибки от API платформы (если Status == Failed).</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>Текст ошибки от API платформы.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Количество попыток публикации (включая retry).</summary>
    public int AttemptCount { get; private set; }
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Лог каждой попытки публикации на конкретную платформу.
/// Создаётся каждый раз, когда Hangfire запускает PublishingService.PublishToTargetAsync().
///
/// Незаменим при дебаггинге:
///   "Почему пост не ушёл на TikTok в 14:00? Смотрим PublishingJob #3 → RawApiResponse."
/// </summary>
public class PublishingJob
{
    public Guid Id { get; private set; }
    public Guid PostTargetId { get; private set; }

    /// <summary>
    /// ID job'а в Hangfire.
    /// Для корреляции с логами планировщика в Hangfire Dashboard.
    /// </summary>
    public string? SchedulerJobId { get; private set; }

    /// <summary>Номер попытки: 1 — первая, 2, 3 — retry после ошибок.</summary>
    public int AttemptNumber { get; private set; }

    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Итог выполнения job'а.</summary>
    public JobOutcome Outcome { get; private set; }

    /// <summary>
    /// Полный stack trace при ошибке.
    /// Только для внутреннего аудита — не передаётся в API-ответах.
    /// </summary>
    public string? ErrorDetails { get; private set; }

    /// <summary>
    /// Сырой JSON-ответ от API платформы.
    /// Сохраняется как для успешных, так и для ошибочных запросов.
    /// Критически полезен при отладке неочевидных ошибок.
    /// </summary>
    public string? RawApiResponse { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public PostTarget PostTarget { get; private set; } = default!;
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Снимок метрик конкретного опубликованного поста.
/// Собирается по расписанию через API платформ:
///   через 1ч → 24ч → 7д → 30д после публикации.
///
/// Связан с PostTarget (а не с Post), потому что метрики разные
/// для каждой платформы публикации.
/// </summary>
public class PostAnalyticsSnapshot
{
    public Guid Id { get; private set; }
    public Guid PostTargetId { get; private set; }

    /// <summary>Момент сбора метрик (UTC).</summary>
    public DateTime RecordedAt { get; private set; }

    public long Views { get; private set; }
    public long Likes { get; private set; }
    public long Comments { get; private set; }
    public long Shares { get; private set; }

    /// <summary>Сохранения / bookmarks (Instagram, TikTok, YouTube).</summary>
    public long Saves { get; private set; }

    /// <summary>Уникальный охват (если платформа предоставляет).</summary>
    public long? Reach { get; private set; }

    /// <summary>Суммарные показы.</summary>
    public long? Impressions { get; private set; }

    /// <summary>Среднее время просмотра в секундах.</summary>
    public double? AverageWatchTime { get; private set; }

    /// <summary>Процент просмотров до конца видео (0.0 – 1.0).</summary>
    public double? CompletionRate { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public PostTarget PostTarget { get; private set; } = default!;
}

// ════════════════════════════════════════════════════════════════════════════
//  БЛОК V — Unified Inbox
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Aggregate Root. Единица общения — переписка (DM) или цепочка комментариев.
/// Объединяет сообщения от одного внешнего пользователя в одном месте,
/// независимо от платформы.
///
/// Типы:
///   DirectMessage — личная переписка
///   Comment       — цепочка комментариев под постом
///   MentionReply  — ответ на упоминание в Stories
///
/// Ключевой флаг IsFollowingUs используется логикой PendingDMQueue:
///   если аккаунт приватный и пользователь не подписан — DM недоступен.
/// </summary>
public class InboxConversation
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid SocialAccountId { get; private set; }

    /// <summary>Тип переписки: DirectMessage, Comment, MentionReply.</summary>
    public ConversationType Type { get; private set; }

    /// <summary>ID этой переписки в системе платформы.</summary>
    public string ExternalConversationId { get; private set; } = default!;

    /// <summary>ID внешнего пользователя на платформе.</summary>
    public string ExternalUserId { get; private set; } = default!;

    /// <summary>Username внешнего пользователя (для отображения).</summary>
    public string? ExternalUserName { get; private set; }

    /// <summary>URL аватара внешнего пользователя.</summary>
    public string? ExternalUserAvatarUrl { get; private set; }

    /// <summary>
    /// Подписан ли внешний пользователь на наш аккаунт.
    /// Критично для PendingDMQueue: если аккаунт приватный и IsFollowingUs == false
    /// → DM откладывается до момента подписки.
    /// </summary>
    public bool IsFollowingUs { get; private set; }

    /// <summary>Время последней проверки статуса подписки.</summary>
    public DateTime? IsFollowingUsCheckedAt { get; private set; }

    /// <summary>PostTarget, к которому относится этот комментарий (только для Type == Comment).</summary>
    public Guid? PostTargetId { get; private set; }

    /// <summary>ID поста на платформе (для построения ссылки).</summary>
    public string? ExternalPostId { get; private set; }

    /// <summary>Превью последнего сообщения для отображения в списке диалогов.</summary>
    public string? LastMessagePreview { get; private set; }

    /// <summary>Время последнего сообщения — для сортировки списка.</summary>
    public DateTime? LastMessageAt { get; private set; }

    /// <summary>Статус диалога для менеджеров Inbox: Open, Resolved, Snoozed.</summary>
    public ConversationStatus Status { get; private set; }

    /// <summary>Количество непрочитанных сообщений — для badge-счётчика в UI.</summary>
    public int UnreadCount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Workspace Workspace { get; private set; } = default!;
    public SocialAccount SocialAccount { get; private set; } = default!;

    /// <summary>Пост, к которому относится цепочка комментариев.</summary>
    public PostTarget? PostTarget { get; private set; }

    /// <summary>Все сообщения переписки (хронологически).</summary>
    public IReadOnlyCollection<InboxMessage> Messages { get; private set; }
        = new List<InboxMessage>();

    /// <summary>Текущее назначение на менеджера (null = не назначен).</summary>
    public ConversationAssignment? Assignment { get; private set; }
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Одно сообщение внутри переписки.
///
/// Может быть:
///   Inbound   — входящее от внешнего пользователя (получено через webhook)
///   Outbound  — исходящее от менеджера команды (отправлено через UI)
///   Automated — исходящее, сгенерированное AutomationRule (IsAutomated == true)
/// </summary>
public class InboxMessage
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }

    /// <summary>
    /// ID сообщения в системе платформы.
    /// Используется для reply, delete и дедупликации при повторных webhook.
    /// </summary>
    public string ExternalMessageId { get; private set; } = default!;

    /// <summary>Направление: Inbound (от внешнего пользователя) или Outbound (от нас).</summary>
    public MessageDirection Direction { get; private set; }

    /// <summary>
    /// Кто отправил сообщение со стороны команды.
    /// null если Direction == Inbound или IsAutomated == true.
    /// </summary>
    public Guid? SentByUserId { get; private set; }

    /// <summary>Признак: это сообщение было отправлено AutomationRule, а не вручную.</summary>
    public bool IsAutomated { get; private set; }

    /// <summary>Правило автоматизации, которое отправило это сообщение.</summary>
    public Guid? AutomationRuleId { get; private set; }

    /// <summary>Тип контента сообщения.</summary>
    public MessageContentType ContentType { get; private set; }

    /// <summary>Текстовое содержание сообщения.</summary>
    public string? TextContent { get; private set; }

    /// <summary>URL медиа-вложения (если ContentType != Text).</summary>
    public string? MediaUrl { get; private set; }

    /// <summary>Время отправки по данным платформы (UTC).</summary>
    public DateTime SentAt { get; private set; }

    /// <summary>Прочитано ли сообщение кем-либо из команды.</summary>
    public bool IsReadByTeam { get; private set; }

    /// <summary>Время прочтения менеджером.</summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>Статус доставки для исходящих сообщений (null для Inbound).</summary>
    public MessageDeliveryStatus? DeliveryStatus { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public InboxConversation Conversation { get; private set; } = default!;
    public ApplicationUser? SentBy { get; private set; }
    public AutomationRule? AutomationRule { get; private set; }
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Назначение диалога конкретному менеджеру команды.
/// Позволяет распределять нагрузку поддержки между сотрудниками.
/// У одного диалога — один активный Assignment (один ответственный).
/// </summary>
public class ConversationAssignment
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }

    /// <summary>Менеджер, которому назначен диалог.</summary>
    public Guid AssignedToUserId { get; private set; }

    /// <summary>
    /// Кто выполнил назначение.
    /// null — если назначение произошло автоматически (через AutomationRule).
    /// </summary>
    public Guid? AssignedByUserId { get; private set; }

    public DateTime AssignedAt { get; private set; }

    /// <summary>Комментарий при назначении: "Срочно, клиент VIP".</summary>
    public string? Note { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public InboxConversation Conversation { get; private set; } = default!;
    public ApplicationUser AssignedTo { get; private set; } = default!;
    public ApplicationUser? AssignedBy { get; private set; }
}

// ════════════════════════════════════════════════════════════════════════════
//  БЛОК VI — Автоматизация (DM Automation)
//  Архитектурный паттерн: Trigger → Condition(s) → Action(s)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Aggregate Root. Правило автоматизации целиком.
///
/// Пример правила:
///   "Когда кто-то оставляет комментарий со словом 'цена' под любым постом
///    в Instagram (@myshop) — отправить им DM: 'Привет! Вот прайс: {{link}}'"
///
/// Правило может быть включено/выключено без удаления (IsEnabled).
/// Защита от спама:
///   MaxActionsPerUser  — не отправлять DM одному человеку более N раз
///   DailyExecutionLimit — не более N срабатываний в день глобально
/// </summary>
public class AutomationRule
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }

    /// <summary>SocialAccount, к которому привязано правило.</summary>
    public Guid SocialAccountId { get; private set; }

    /// <summary>Название правила в UI: "DM по ключевому слову #цена".</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Описание для команды: зачем это правило и как оно работает.</summary>
    public string? Description { get; private set; }

    /// <summary>Включено ли правило. Выключенное правило не срабатывает, но не удаляется.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Тип события, запускающего правило.</summary>
    public AutomationTriggerType TriggerType { get; private set; }

    /// <summary>
    /// ID поста на платформе, к которому привязано правило.
    /// null = правило применяется ко всем постам аккаунта.
    /// </summary>
    public string? TargetExternalPostId { get; private set; }

    /// <summary>
    /// Максимальное число срабатываний правила для одного внешнего пользователя.
    /// 0 = без ограничений, 1 = только один раз (рекомендуется для SendDM).
    /// </summary>
    public int MaxActionsPerUser { get; private set; }

    /// <summary>Глобальный дневной лимит срабатываний (защита от массового спама).</summary>
    public int? DailyExecutionLimit { get; private set; }

    /// <summary>Счётчик срабатываний сегодня. Сбрасывается midnight Hangfire job'ом.</summary>
    public int TodayExecutionCount { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Workspace Workspace { get; private set; } = default!;
    public SocialAccount SocialAccount { get; private set; } = default!;

    /// <summary>Условия срабатывания (AND-логика: все условия должны выполниться).</summary>
    public IReadOnlyCollection<TriggerCondition> Conditions { get; private set; }
        = new List<TriggerCondition>();

    /// <summary>Действия при срабатывании (выполняются в порядке ExecutionOrder).</summary>
    public IReadOnlyCollection<AutomationAction> Actions { get; private set; }
        = new List<AutomationAction>();

    /// <summary>История срабатываний для аналитики и дебаггинга.</summary>
    public IReadOnlyCollection<AutomationExecutionLog> ExecutionLogs { get; private set; }
        = new List<AutomationExecutionLog>();
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Одно условие для срабатывания AutomationRule.
/// Несколько условий в одном правиле работают по AND-логике
/// (все условия должны выполниться одновременно).
///
/// Примеры:
///   CommentText Contains "цена"
///   CommentText Contains "скидка"
///   → правило сработает только если комментарий содержит ОБА слова
/// </summary>
public class TriggerCondition
{
    public Guid Id { get; private set; }
    public Guid AutomationRuleId { get; private set; }

    /// <summary>Тип проверяемого условия.</summary>
    public ConditionType Type { get; private set; }

    /// <summary>Оператор сравнения.</summary>
    public ConditionOperator Operator { get; private set; }

    /// <summary>
    /// Значение для сравнения: "цена", "скидка", "DM".
    /// null когда Operator == Any (любое значение проходит).
    /// </summary>
    public string? Value { get; private set; }

    /// <summary>
    /// Учитывать ли регистр при сравнении.
    /// Рекомендуется false для большего охвата: "Цена" == "цена".
    /// </summary>
    public bool IsCaseSensitive { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public AutomationRule AutomationRule { get; private set; } = default!;
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Действие, выполняемое при срабатывании AutomationRule.
/// Одно правило может иметь несколько действий, выполняемых в порядке ExecutionOrder.
///
/// Пример цепочки действий:
///   1. LikeComment      (мгновенно)
///   2. SendDirectMessage (через 45 секунд — не выглядит как бот)
///   3. AddConversationTag (тег "лид" в Inbox)
///
/// Шаблон сообщения поддерживает переменные:
///   {{username}}     — имя пользователя
///   {{post_url}}     — ссылка на пост
///   {{comment_text}} — текст исходного комментария
///   {{link}}         — значение из поля LinkUrl
/// </summary>
public class AutomationAction
{
    public Guid Id { get; private set; }
    public Guid AutomationRuleId { get; private set; }

    /// <summary>Тип выполняемого действия.</summary>
    public ActionType Type { get; private set; }

    /// <summary>Порядок выполнения при наличии нескольких действий (1, 2, 3...).</summary>
    public int ExecutionOrder { get; private set; }

    /// <summary>
    /// Задержка перед выполнением действия в секундах.
    /// Рекомендуется 30–60 сек для SendDM — имитирует человеческую реакцию.
    /// </summary>
    public int DelaySeconds { get; private set; }

    /// <summary>
    /// Шаблон сообщения для SendDM / ReplyToComment.
    /// Поддерживает переменные: {{username}}, {{post_url}}, {{comment_text}}, {{link}}.
    /// </summary>
    public string? MessageTemplate { get; private set; }

    /// <summary>
    /// Конкретная ссылка, которая вставляется вместо {{link}} в шаблоне.
    /// Пример: "https://example.com/promo"
    /// </summary>
    public string? LinkUrl { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public AutomationRule AutomationRule { get; private set; } = default!;
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// ⭐ Очередь отложенных DM.
///
/// Ключевая сущность для работы с приватными аккаунтами.
/// Instagram API не позволяет отправить DM пользователю, если:
///   - его аккаунт приватный и он НЕ подписан на наш аккаунт
///   - превышен Rate Limit API
///   - пользователь отключил приём DM
///
/// В этих случаях DM помещается в очередь.
/// Hangfire job каждые 30 минут обходит Waiting-записи и проверяет,
/// изменились ли условия. При появлении подписчика — отправляет DM.
///
/// Срок ожидания ограничен: ExpiresAt (по умолчанию 7 дней).
/// По истечении — Status меняется на Expired.
/// </summary>
public class PendingDMQueue
{
    public Guid Id { get; private set; }
    public Guid AutomationRuleId { get; private set; }
    public Guid SocialAccountId { get; private set; }

    /// <summary>ID внешнего пользователя, которому нужно отправить DM.</summary>
    public string ExternalUserId { get; private set; } = default!;

    /// <summary>Username для отображения в UI мониторинга.</summary>
    public string? ExternalUserName { get; private set; }

    /// <summary>
    /// Финальный текст сообщения с уже подставленными переменными.
    /// Подстановка выполняется в момент срабатывания триггера, не в момент отправки.
    /// </summary>
    public string ResolvedMessageText { get; private set; } = default!;

    /// <summary>Причина, по которой DM не был отправлен сразу.</summary>
    public PendingReason Reason { get; private set; }

    /// <summary>Момент изначального срабатывания триггера (UTC).</summary>
    public DateTime TriggeredAt { get; private set; }

    /// <summary>Время последней проверки статуса подписки (UTC).</summary>
    public DateTime? LastCheckedAt { get; private set; }

    /// <summary>Количество выполненных проверок подписки.</summary>
    public int CheckAttemptCount { get; private set; }

    /// <summary>Дата истечения ожидания. По умолчанию: TriggeredAt + 7 дней.</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Текущий статус записи в очереди.</summary>
    public PendingDMStatus Status { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public AutomationRule AutomationRule { get; private set; } = default!;
    public SocialAccount SocialAccount { get; private set; } = default!;
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Детальный лог каждого срабатывания AutomationRule.
///
/// Используется для:
///   - Дебаггинга: "почему правило не сработало?"
///   - Защиты от дублей: проверка ExternalTriggerEventId перед выполнением
///   - Аналитики: сколько DM отправлено, сколько пропущено, эффективность
///
/// Идемпотентность: при получении одного и того же ExternalTriggerEventId
/// повторно — возвращаем Skipped, не выполняем повторно.
/// </summary>
public class AutomationExecutionLog
{
    public Guid Id { get; private set; }
    public Guid AutomationRuleId { get; private set; }

    /// <summary>
    /// ID события на платформе, вызвавшего срабатывание.
    /// Для комментария — ID комментария, для подписки — ID подписчика.
    /// Используется как ключ идемпотентности.
    /// </summary>
    public string ExternalTriggerEventId { get; private set; } = default!;

    /// <summary>Внешний пользователь, чьё действие вызвало триггер.</summary>
    public string TriggerExternalUserId { get; private set; } = default!;

    /// <summary>Username пользователя на момент срабатывания.</summary>
    public string? TriggerExternalUserName { get; private set; }

    /// <summary>Текст комментария / сообщения, вызвавшего триггер.</summary>
    public string? TriggerContent { get; private set; }

    public DateTime ExecutedAt { get; private set; }

    /// <summary>Итог выполнения правила.</summary>
    public AutomationExecutionOutcome Outcome { get; private set; }

    /// <summary>
    /// Причина пропуска (если Outcome == Skipped).
    /// Примеры: "MaxActionsPerUser reached", "DailyExecutionLimit reached",
    ///          "Duplicate event", "Condition not met".
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>Текст ошибки API (если Outcome == Failed).</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Ссылка на запись PendingDMQueue (если Outcome == Pending).</summary>
    public Guid? PendingDMQueueId { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public AutomationRule AutomationRule { get; private set; } = default!;
}

// ════════════════════════════════════════════════════════════════════════════
//  БЛОК VII — Инфраструктурные Сущности
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Буфер для входящих Webhook-событий от социальных платформ.
///
/// Платформы отправляют события асинхронно через HTTP POST:
///   новый комментарий, новый DM, новый подписчик, реакция на пост.
///
/// Критическое правило: ответить 200 OK в течение 200ms.
/// Если ответ задерживается — платформа повторяет попытки и может заблокировать webhook.
///
/// Поэтому процесс двухэтапный:
///   1. Верифицировать подпись → сохранить RawPayload → ответить 200 OK (мгновенно)
///   2. Hangfire job: прочитать WebhookEvent → обработать → создать InboxMessage
///
/// Навигационных свойств нет — это инфраструктурная сущность, изолированная от домена.
/// </summary>
public class WebhookEvent
{
    public Guid Id { get; private set; }

    /// <summary>Платформа-источник события.</summary>
    public Platform Platform { get; private set; }

    /// <summary>Тип события: "comment", "message", "follow", "mention".</summary>
    public string EventType { get; private set; } = default!;

    /// <summary>
    /// Сырой JSON payload от платформы.
    /// Сохраняется немедленно до любой обработки.
    /// Позволяет повторно обработать событие при сбое.
    /// </summary>
    public string RawPayload { get; private set; } = default!;

    /// <summary>
    /// Криптографическая подпись для верификации (X-Hub-Signature-256 и аналоги).
    /// Проверяется через HMAC-SHA256 с App Secret платформы.
    /// </summary>
    public string? Signature { get; private set; }

    /// <summary>Подпись прошла верификацию. false = событие отклонено как поддельное.</summary>
    public bool IsVerified { get; private set; }

    /// <summary>Время получения события (UTC).</summary>
    public DateTime ReceivedAt { get; private set; }

    /// <summary>Статус обработки.</summary>
    public WebhookEventStatus Status { get; private set; }

    /// <summary>Количество попыток обработки (для retry-логики Hangfire).</summary>
    public int ProcessingAttemptCount { get; private set; }

    /// <summary>Время успешной или финально-неуспешной обработки.</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>Детали ошибки при обработке.</summary>
    public string? ProcessingError { get; private set; }
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Настройки уведомлений конкретного члена команды в конкретном Workspace.
///
/// Определяет, о каких событиях и через какие каналы уведомлять пользователя:
///   InAppEnabled  — всплывающее уведомление в браузере / приложении
///   EmailEnabled  — письмо на email пользователя
///   PushEnabled   — push-уведомление в мобильном приложении
/// </summary>
public class NotificationPreference
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }

    /// <summary>Тип события, для которого настроено уведомление.</summary>
    public NotificationEventType EventType { get; private set; }

    /// <summary>Уведомлять в интерфейсе (SignalR / real-time badge).</summary>
    public bool InAppEnabled { get; private set; }

    /// <summary>Отправлять email-уведомление.</summary>
    public bool EmailEnabled { get; private set; }

    /// <summary>Отправлять push-уведомление в мобильное приложение.</summary>
    public bool PushEnabled { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public ApplicationUser User { get; private set; } = default!;
}

// ════════════════════════════════════════════════════════════════════════════
//  ENUMS — Все перечисления
// ════════════════════════════════════════════════════════════════════════════

// ── Workspace & Auth ─────────────────────────────────────────────────────────

/// <summary>Тарифный план Workspace.</summary>
public enum SubscriptionPlan
{
    Free,
    Pro,
    Business,
    Enterprise
}

/// <summary>Роль участника в Workspace (RBAC).</summary>
public enum WorkspaceRole
{
    /// <summary>Полный доступ, включая billing и удаление Workspace.</summary>
    Owner,

    /// <summary>Управление командой и подключёнными аккаунтами.</summary>
    Admin,

    /// <summary>Создание и редактирование постов.</summary>
    Editor,

    /// <summary>Только просмотр: аналитика, Inbox (read-only).</summary>
    Viewer
}

/// <summary>Статус участника Workspace.</summary>
public enum MemberStatus
{
    /// <summary>Приглашение отправлено, ещё не принято.</summary>
    Invited,

    /// <summary>Участник принял приглашение и активен.</summary>
    Active,

    /// <summary>Доступ временно приостановлен (не удалён).</summary>
    Suspended
}

// ── Social Accounts ──────────────────────────────────────────────────────────

/// <summary>Поддерживаемые социальные платформы.</summary>
public enum Platform
{
    YouTube,
    Instagram,
    Facebook,
    TikTok,
    Twitter,
    Telegram
}

/// <summary>Статус подключённого социального аккаунта.</summary>
public enum SocialAccountStatus
{
    /// <summary>Токены действительны, аккаунт работает нормально.</summary>
    Active,

    /// <summary>Refresh Token истёк или отозван. Требуется переподключение.</summary>
    TokenExpired,

    /// <summary>Пользователь отключил аккаунт вручную.</summary>
    Disconnected,

    /// <summary>Платформа отозвала доступ (пользователь удалил приложение и т.д.).</summary>
    Revoked
}

// ── Video ────────────────────────────────────────────────────────────────────

/// <summary>Статус обработки видеофайла.</summary>
public enum VideoStatus
{
    /// <summary>Chunked upload ещё в процессе.</summary>
    Uploading,

    /// <summary>Файл загружен, идёт анализ через FFprobe и генерация thumbnail.</summary>
    Processing,

    /// <summary>Файл готов к использованию в постах.</summary>
    Ready,

    /// <summary>Ошибка при загрузке или обработке. Детали в ProcessingError.</summary>
    Failed,

    /// <summary>Помечен на удаление (soft delete). Физически удаляется job'ом.</summary>
    Deleted
}

// ── Post ─────────────────────────────────────────────────────────────────────

/// <summary>Видимость публикации.</summary>
public enum Visibility
{
    /// <summary>Публичная — доступна всем.</summary>
    Public,

    /// <summary>По ссылке — не отображается в поиске и профиле.</summary>
    Unlisted,

    /// <summary>Приватная — только для автора.</summary>
    Private
}

/// <summary>Статус жизненного цикла поста.</summary>
public enum PostStatus
{
    /// <summary>Черновик — не запланирован, можно редактировать.</summary>
    Draft,

    /// <summary>Запланирован — Hangfire job зарегистрирован, ожидает времени.</summary>
    Scheduled,

    /// <summary>Сейчас публикуется — PublishingService выполняется.</summary>
    Publishing,

    /// <summary>Опубликован успешно на всех целевых платформах.</summary>
    Published,

    /// <summary>Часть платформ не прошла — не все PostTarget успешны.</summary>
    PartiallyFailed,

    /// <summary>Все платформы вернули ошибку.</summary>
    Failed,

    /// <summary>Отменён пользователем до начала публикации.</summary>
    Cancelled
}

/// <summary>Статус публикации на конкретную платформу (PostTarget).</summary>
public enum TargetStatus
{
    /// <summary>Ожидает запуска Hangfire job'а.</summary>
    Pending,

    /// <summary>PublishingService сейчас выполняет запрос к API платформы.</summary>
    Publishing,

    /// <summary>Успешно опубликовано.</summary>
    Published,

    /// <summary>Ошибка публикации (все retry исчерпаны).</summary>
    Failed,

    /// <summary>Ожидает повторной попытки (Polly retry в процессе).</summary>
    Retrying
}

/// <summary>Итог выполнения PublishingJob.</summary>
public enum JobOutcome
{
    /// <summary>Job выполняется прямо сейчас.</summary>
    InProgress,

    /// <summary>Публикация прошла успешно.</summary>
    Succeeded,

    /// <summary>Ошибка при публикации.</summary>
    Failed,

    /// <summary>Job завершился, Polly выполнит повторную попытку.</summary>
    Retrying
}

// ── Inbox ────────────────────────────────────────────────────────────────────

/// <summary>Тип переписки в Inbox.</summary>
public enum ConversationType
{
    /// <summary>Личное сообщение (Direct Message).</summary>
    DirectMessage,

    /// <summary>Комментарий под постом.</summary>
    Comment,

    /// <summary>Упоминание в Stories с возможностью ответа.</summary>
    MentionReply
}

/// <summary>Статус диалога для управления в Inbox.</summary>
public enum ConversationStatus
{
    /// <summary>Открытый — требует внимания.</summary>
    Open,

    /// <summary>Решён — закрыт менеджером.</summary>
    Resolved,

    /// <summary>Отложен — напомнить позже (snooze).</summary>
    Snoozed
}

/// <summary>Направление сообщения.</summary>
public enum MessageDirection
{
    /// <summary>Входящее — от внешнего пользователя.</summary>
    Inbound,

    /// <summary>Исходящее — от команды (ручное или автоматическое).</summary>
    Outbound
}

/// <summary>Тип контента сообщения.</summary>
public enum MessageContentType
{
    Text,
    Image,
    Video,
    Audio,
    Story,
    Sticker,
    Reaction,

    /// <summary>Тип контента, не поддерживаемый текущей версией системы.</summary>
    Unsupported
}

/// <summary>Статус доставки исходящего сообщения.</summary>
public enum MessageDeliveryStatus
{
    /// <summary>Запрос отправлен в API платформы, ожидаем ответа.</summary>
    Sending,

    /// <summary>API подтвердил отправку.</summary>
    Sent,

    /// <summary>Сообщение доставлено в устройство получателя.</summary>
    Delivered,

    /// <summary>Получатель прочитал сообщение.</summary>
    Read,

    /// <summary>Ошибка отправки.</summary>
    Failed
}

// ── Automation ───────────────────────────────────────────────────────────────

/// <summary>Тип события, запускающего AutomationRule.</summary>
public enum AutomationTriggerType
{
    /// <summary>Новый комментарий под любым постом аккаунта.</summary>
    NewComment,

    /// <summary>Новый подписчик аккаунта.</summary>
    NewFollower,

    /// <summary>Упоминание аккаунта в Stories другого пользователя.</summary>
    StoryMention,

    /// <summary>Входящий DM с ключевым словом.</summary>
    DirectMessageReceived,

    /// <summary>Комментарий, содержащий конкретное ключевое слово.</summary>
    CommentKeyword
}

/// <summary>Тип проверяемого условия в TriggerCondition.</summary>
public enum ConditionType
{
    /// <summary>Проверка текста комментария.</summary>
    CommentText,

    /// <summary>Автор комментария подписан на наш аккаунт.</summary>
    CommentAuthorIsFollower,

    /// <summary>Аккаунт автора комментария является публичным.</summary>
    AccountIsPublic,

    /// <summary>Пользователь комментирует этот аккаунт впервые.</summary>
    FirstTimeCommenter
}

/// <summary>Оператор сравнения для TriggerCondition.</summary>
public enum ConditionOperator
{
    /// <summary>Значение содержит подстроку.</summary>
    Contains,

    /// <summary>Значение точно равно.</summary>
    Equals,

    /// <summary>Значение начинается с подстроки.</summary>
    StartsWith,

    /// <summary>Значение заканчивается на подстроку.</summary>
    EndsWith,

    /// <summary>Любое значение — условие всегда выполняется.</summary>
    Any,

    /// <summary>Значение НЕ содержит подстроку.</summary>
    NotContains
}

/// <summary>Тип действия AutomationAction.</summary>
public enum ActionType
{
    /// <summary>Отправить Direct Message — главное действие DM Automation.</summary>
    SendDirectMessage,

    /// <summary>Поставить лайк на комментарий.</summary>
    LikeComment,

    /// <summary>Ответить на комментарий публично.</summary>
    ReplyToComment,

    /// <summary>Добавить тег к диалогу в Unified Inbox.</summary>
    AddConversationTag,

    /// <summary>Назначить диалог конкретному менеджеру команды.</summary>
    AssignToTeamMember
}

/// <summary>Причина помещения DM в PendingDMQueue.</summary>
public enum PendingReason
{
    /// <summary>Аккаунт получателя приватный — нужна подписка.</summary>
    TargetAccountIsPrivate,

    /// <summary>Превышен Rate Limit API платформы — ждём сброса.</summary>
    ApiRateLimitReached,

    /// <summary>Пользователь отключил приём DM.</summary>
    DMsDisabledByUser
}

/// <summary>Статус записи в PendingDMQueue.</summary>
public enum PendingDMStatus
{
    /// <summary>Ожидает выполнения условия (подписки, сброса Rate Limit и т.д.).</summary>
    Waiting,

    /// <summary>DM успешно отправлен.</summary>
    Sent,

    /// <summary>Истёк срок ожидания (ExpiresAt прошёл).</summary>
    Expired,

    /// <summary>Отменён вручную менеджером.</summary>
    Cancelled
}

/// <summary>Итог выполнения AutomationRule.</summary>
public enum AutomationExecutionOutcome
{
    /// <summary>Все действия выполнены успешно.</summary>
    Executed,

    /// <summary>Выполнение пропущено (лимит, дубликат, условие не выполнено).</summary>
    Skipped,

    /// <summary>DM помещён в очередь ожидания (PendingDMQueue).</summary>
    Pending,

    /// <summary>Ошибка при вызове API платформы.</summary>
    Failed
}

// ── Infrastructure ────────────────────────────────────────────────────────────

/// <summary>Статус обработки WebhookEvent.</summary>
public enum WebhookEventStatus
{
    /// <summary>Получен, ещё не обработан.</summary>
    Received,

    /// <summary>Обрабатывается Hangfire job'ом прямо сейчас.</summary>
    Processing,

    /// <summary>Успешно обработан.</summary>
    Processed,

    /// <summary>Ошибка при обработке (все retry исчерпаны).</summary>
    Failed,

    /// <summary>Проигнорирован (не прошёл верификацию подписи, дубликат и т.д.).</summary>
    Ignored
}

/// <summary>Тип события для настройки уведомлений.</summary>
public enum NotificationEventType
{
    /// <summary>Пост успешно опубликован на всех платформах.</summary>
    PostPublished,

    /// <summary>Публикация поста завершилась ошибкой.</summary>
    PostFailed,

    /// <summary>Новое входящее сообщение в Unified Inbox.</summary>
    NewInboxMessage,

    /// <summary>Правило автоматизации сработало.</summary>
    AutomationTriggered,

    /// <summary>Социальный аккаунт отключён (токен истёк или отозван).</summary>
    SocialAccountDisconnected,

    /// <summary>Новый участник приглашён в Workspace.</summary>
    TeamMemberInvited
}
