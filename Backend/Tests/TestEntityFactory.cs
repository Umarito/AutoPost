using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Tests;

/// <summary>
/// Provides factory methods for creating domain entities in tests.
///
/// <para><b>Why:</b>
/// Domain entities use <c>private set;</c> on all properties (DDD encapsulation).
/// EF Core's InMemory provider bypasses these restrictions when reading from the database,
/// but we need a way to create properly initialized entities for seeding test data.
/// This helper uses EF Core's ability to set private properties via the context tracker.</para>
///
/// <para><b>Pattern:</b>
/// Each method creates an entity with sensible defaults. Callers can override specific
/// properties via optional parameters to test specific scenarios (e.g., expired tokens,
/// inactive accounts, specific platforms).</para>
/// </summary>
internal static class TestEntityFactory
{
    /// <summary>
    /// Creates an <see cref="ApplicationUser"/> with all required fields set.
    /// Identity fields (Id, Email, UserName) are set directly since they have public setters.
    /// </summary>
    public static ApplicationUser CreateUser(
        Guid? id = null,
        string displayName = "Test User",
        string email = "test@autopost.com",
        bool isActive = true)
    {
        // Use the domain factory method to ensure IsActive, DisplayName and other
        // private-set properties are correctly initialised via the DDD encapsulation path.
        var user = ApplicationUser.Create(email, displayName, DateTime.UtcNow);

        // Override the auto-generated Id when callers supply one.
        if (id.HasValue)
        {
            user.Id = id.Value;
        }

        // Identity-managed fields needed by UserManager mock setups.
        user.NormalizedEmail = email.ToUpperInvariant();
        user.NormalizedUserName = email.ToUpperInvariant();
        user.SecurityStamp = Guid.NewGuid().ToString();

        // Support deactivation scenarios in tests.
        if (!isActive)
        {
            user.Deactivate();
        }

        return user;
    }

    /// <summary>
    /// Seeds a user with private set properties via EF Core context tracking.
    /// This is the recommended way to create test users because EF Core's InMemory
    /// provider can write to private set properties through the change tracker.
    /// </summary>
    public static async Task<ApplicationUser> SeedUserAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid? id = null,
        string displayName = "Test User",
        string email = "test@autopost.com",
        bool isActive = true)
    {
        var user = CreateUser(id, displayName, email, isActive);
        var entry = ctx.Entry(user);
        entry.Property("DisplayName").CurrentValue = displayName;
        entry.Property("IsActive").CurrentValue = isActive;
        entry.Property("RegisteredAt").CurrentValue = DateTime.UtcNow;
        entry.Property("TimeZoneId").CurrentValue = "UTC";
        entry.Property("Locale").CurrentValue = "en";
        ctx.ApplicationUsers.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Seeds a <see cref="Workspace"/> with private set properties set via EF Core tracker.
    /// </summary>
    public static async Task<Workspace> SeedWorkspaceAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid? id = null,
        string name = "Test Workspace",
        string slug = "test-workspace",
        SubscriptionPlan plan = SubscriptionPlan.Free,
        bool isActive = true)
    {
        var workspace = new Workspace();
        var entry = ctx.Entry(workspace);
        entry.Property("Id").CurrentValue = id ?? Guid.NewGuid();
        entry.Property("Name").CurrentValue = name;
        entry.Property("Slug").CurrentValue = slug;
        entry.Property("Plan").CurrentValue = plan;
        entry.Property("MaxSocialAccounts").CurrentValue = 5;
        entry.Property("MaxTeamMembers").CurrentValue = 10;
        entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        entry.Property("IsActive").CurrentValue = isActive;
        ctx.Workspaces.Add(workspace);
        await ctx.SaveChangesAsync();
        return workspace;
    }

    /// <summary>
    /// Seeds a <see cref="WorkspaceMember"/> linking a user to a workspace with a role.
    /// </summary>
    public static async Task<WorkspaceMember> SeedWorkspaceMemberAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid workspaceId,
        Guid userId,
        WorkspaceRole role = WorkspaceRole.Editor,
        MemberStatus status = MemberStatus.Active)
    {
        var member = new WorkspaceMember();
        var entry = ctx.Entry(member);
        entry.Property("Id").CurrentValue = Guid.NewGuid();
        entry.Property("WorkspaceId").CurrentValue = workspaceId;
        entry.Property("UserId").CurrentValue = userId;
        entry.Property("Role").CurrentValue = role;
        entry.Property("Status").CurrentValue = status;
        entry.Property("InvitedEmail").CurrentValue = "invited@test.com";
        entry.Property("InvitedAt").CurrentValue = DateTime.UtcNow;
        ctx.WorkspaceMembers.Add(member);
        await ctx.SaveChangesAsync();
        return member;
    }

    /// <summary>
    /// Seeds a <see cref="RefreshToken"/> for authentication flow testing.
    /// </summary>
    public static async Task<RefreshToken> SeedRefreshTokenAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid userId,
        string tokenHash = "test-hash-123",
        bool isUsed = false,
        bool isRevoked = false,
        DateTime? expiresAt = null)
    {
        var token = new RefreshToken();
        var entry = ctx.Entry(token);
        entry.Property("Id").CurrentValue = Guid.NewGuid();
        entry.Property("UserId").CurrentValue = userId;
        entry.Property("TokenHash").CurrentValue = tokenHash;
        entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        entry.Property("ExpiresAt").CurrentValue = expiresAt ?? DateTime.UtcNow.AddDays(7);
        entry.Property("IsUsed").CurrentValue = isUsed;
        entry.Property("IsRevoked").CurrentValue = isRevoked;
        ctx.RefreshTokens.Add(token);
        await ctx.SaveChangesAsync();
        return token;
    }

    /// <summary>
    /// Seeds a <see cref="SocialAccount"/> connected to a workspace.
    /// </summary>
    public static async Task<SocialAccount> SeedSocialAccountAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid workspaceId,
        Guid? id = null,
        Platform platform = Platform.Instagram,
        string externalId = "ext-123",
        SocialAccountStatus status = SocialAccountStatus.Active)
    {
        var account = new SocialAccount();
        var entry = ctx.Entry(account);
        entry.Property("Id").CurrentValue = id ?? Guid.NewGuid();
        entry.Property("WorkspaceId").CurrentValue = workspaceId;
        entry.Property("Platform").CurrentValue = platform;
        entry.Property("ExternalAccountId").CurrentValue = externalId;
        entry.Property("AccountDisplayName").CurrentValue = "Test Account";
        entry.Property("EncryptedAccessToken").CurrentValue = "encrypted-token";
        entry.Property("TokenExpiresAt").CurrentValue = DateTime.UtcNow.AddDays(60);
        entry.Property("GrantedScopes").CurrentValue = "read,write";
        entry.Property("Status").CurrentValue = status;
        entry.Property("IsPrivateAccount").CurrentValue = false;
        entry.Property("ConnectedAt").CurrentValue = DateTime.UtcNow;
        ctx.SocialAccounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    /// <summary>
    /// Seeds a <see cref="Video"/> in the workspace media library.
    /// </summary>
    public static async Task<Video> SeedVideoAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid workspaceId,
        Guid uploadedByUserId,
        Guid? id = null,
        VideoStatus status = VideoStatus.Ready)
    {
        var video = new Video();
        var entry = ctx.Entry(video);
        entry.Property("Id").CurrentValue = id ?? Guid.NewGuid();
        entry.Property("WorkspaceId").CurrentValue = workspaceId;
        entry.Property("UploadedByUserId").CurrentValue = uploadedByUserId;
        entry.Property("StorageKey").CurrentValue = $"videos/{Guid.NewGuid()}.mp4";
        entry.Property("OriginalFileName").CurrentValue = "test-video.mp4";
        entry.Property("ContentType").CurrentValue = "video/mp4";
        entry.Property("FileSizeBytes").CurrentValue = 1024L * 1024;
        entry.Property("Status").CurrentValue = status;
        entry.Property("UploadedAt").CurrentValue = DateTime.UtcNow;
        ctx.Videos.Add(video);
        await ctx.SaveChangesAsync();
        return video;
    }

    /// <summary>
    /// Seeds a <see cref="Post"/> with content and schedule value objects.
    /// </summary>
    public static async Task<Post> SeedPostAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid workspaceId,
        Guid createdByUserId,
        Guid? id = null,
        PostStatus status = PostStatus.Draft,
        DateTime? createdAt = null)
    {
        var post = new Post();
        var entry = ctx.Entry(post);
        var postId = id ?? Guid.NewGuid();
        entry.Property("Id").CurrentValue = postId;
        entry.Property("WorkspaceId").CurrentValue = workspaceId;
        entry.Property("CreatedByUserId").CurrentValue = createdByUserId;
        entry.Property("Status").CurrentValue = status;
        entry.Property("CreatedAt").CurrentValue = createdAt ?? DateTime.UtcNow;

        // Value objects — EF Core owned types set via navigation
        var content = Activator.CreateInstance(typeof(PostContent), true);
        entry.Reference("Content").CurrentValue = content;
        entry.Reference("Content").TargetEntry!.Property("Title").CurrentValue = "Test Post";
        entry.Reference("Content").TargetEntry!.Property("Description").CurrentValue = "Test body content";
        entry.Reference("Content").TargetEntry!.Property("Visibility").CurrentValue = Visibility.Public;

        var schedule = Activator.CreateInstance(typeof(Schedule), true);
        entry.Reference("Schedule").CurrentValue = schedule;
        entry.Reference("Schedule").TargetEntry!.Property("ScheduledAt").CurrentValue = DateTime.UtcNow.AddHours(1);
        entry.Reference("Schedule").TargetEntry!.Property("TimeZoneId").CurrentValue = "UTC";

        ctx.Posts.Add(post);
        await ctx.SaveChangesAsync();
        return post;
    }

    /// <summary>
    /// Seeds a <see cref="PostTarget"/> linking a post to a social account.
    /// </summary>
    public static async Task<PostTarget> SeedPostTargetAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid postId,
        Guid socialAccountId,
        Guid? id = null,
        Platform platform = Platform.Instagram,
        TargetStatus status = TargetStatus.Pending)
    {
        var target = new PostTarget();
        var entry = ctx.Entry(target);
        entry.Property("Id").CurrentValue = id ?? Guid.NewGuid();
        entry.Property("PostId").CurrentValue = postId;
        entry.Property("SocialAccountId").CurrentValue = socialAccountId;
        entry.Property("Platform").CurrentValue = platform;
        entry.Property("Status").CurrentValue = status;
        ctx.PostTargets.Add(target);
        await ctx.SaveChangesAsync();
        return target;
    }

    /// <summary>
    /// Seeds an <see cref="InboxConversation"/> in the workspace unified inbox.
    /// </summary>
    public static async Task<InboxConversation> SeedInboxConversationAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid workspaceId,
        Guid socialAccountId,
        Guid? id = null,
        string externalId = "ext-conv-1",
        ConversationStatus status = ConversationStatus.Open)
    {
        var conv = new InboxConversation();
        var entry = ctx.Entry(conv);
        entry.Property("Id").CurrentValue = id ?? Guid.NewGuid();
        entry.Property("WorkspaceId").CurrentValue = workspaceId;
        entry.Property("SocialAccountId").CurrentValue = socialAccountId;
        entry.Property("Type").CurrentValue = ConversationType.DirectMessage;
        entry.Property("ExternalConversationId").CurrentValue = externalId;
        entry.Property("ExternalUserId").CurrentValue = "ext-user-1";
        entry.Property("IsFollowingUs").CurrentValue = true;
        entry.Property("Status").CurrentValue = status;
        entry.Property("UnreadCount").CurrentValue = 0;
        entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        ctx.InboxConversations.Add(conv);
        await ctx.SaveChangesAsync();
        return conv;
    }

    /// <summary>
    /// Seeds an <see cref="InboxMessage"/> within a conversation.
    /// </summary>
    public static async Task<InboxMessage> SeedInboxMessageAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid conversationId,
        Guid? id = null,
        string externalMessageId = "ext-msg-1",
        MessageDirection direction = MessageDirection.Inbound,
        bool isRead = false)
    {
        var msg = new InboxMessage();
        var entry = ctx.Entry(msg);
        entry.Property("Id").CurrentValue = id ?? Guid.NewGuid();
        entry.Property("ConversationId").CurrentValue = conversationId;
        entry.Property("ExternalMessageId").CurrentValue = externalMessageId;
        entry.Property("Direction").CurrentValue = direction;
        entry.Property("IsAutomated").CurrentValue = false;
        entry.Property("ContentType").CurrentValue = MessageContentType.Text;
        entry.Property("SentAt").CurrentValue = DateTime.UtcNow;
        entry.Property("IsReadByTeam").CurrentValue = isRead;
        ctx.InboxMessages.Add(msg);
        await ctx.SaveChangesAsync();
        return msg;
    }

    /// <summary>
    /// Seeds an <see cref="AutomationRule"/> with its conditions and actions.
    /// </summary>
    public static async Task<AutomationRule> SeedAutomationRuleAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid workspaceId,
        Guid socialAccountId,
        Guid? id = null,
        bool isEnabled = true)
    {
        var rule = new AutomationRule();
        var entry = ctx.Entry(rule);
        entry.Property("Id").CurrentValue = id ?? Guid.NewGuid();
        entry.Property("WorkspaceId").CurrentValue = workspaceId;
        entry.Property("SocialAccountId").CurrentValue = socialAccountId;
        entry.Property("Name").CurrentValue = "Test Rule";
        entry.Property("IsEnabled").CurrentValue = isEnabled;
        entry.Property("TriggerType").CurrentValue = AutomationTriggerType.NewComment;
        entry.Property("MaxActionsPerUser").CurrentValue = 5;
        entry.Property("TodayExecutionCount").CurrentValue = 0;
        ctx.AutomationRules.Add(rule);
        await ctx.SaveChangesAsync();
        return rule;
    }

    /// <summary>
    /// Seeds a <see cref="WebhookEvent"/> for webhook processing tests.
    /// </summary>
    public static async Task<WebhookEvent> SeedWebhookEventAsync(
        Infrastructure.Data.ApplicationDbContext ctx,
        Guid? id = null,
        Platform platform = Platform.Instagram,
        WebhookEventStatus status = WebhookEventStatus.Received,
        bool isVerified = true)
    {
        var evt = new WebhookEvent();
        var entry = ctx.Entry(evt);
        entry.Property("Id").CurrentValue = id ?? Guid.NewGuid();
        entry.Property("Platform").CurrentValue = platform;
        entry.Property("EventType").CurrentValue = "comment.created";
        entry.Property("RawPayload").CurrentValue = "{\"test\": true}";
        entry.Property("IsVerified").CurrentValue = isVerified;
        entry.Property("ReceivedAt").CurrentValue = DateTime.UtcNow;
        entry.Property("Status").CurrentValue = status;
        entry.Property("ProcessingAttemptCount").CurrentValue = 0;
        ctx.WebhookEvents.Add(evt);
        await ctx.SaveChangesAsync();
        return evt;
    }
}
