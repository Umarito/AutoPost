using System.Reflection;
using Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

/// <summary>
/// Represents the main Entity Framework Core database context for the AutoPost platform.
///
/// <para>
/// The context combines ASP.NET Core Identity storage, application domain entities
/// and persisted Data Protection keys inside a single PostgreSQL-backed model.
/// All entity mapping logic is discovered from <c>IEntityTypeConfiguration&lt;T&gt;</c>
/// implementations located in the Infrastructure assembly.
/// </para>
/// </summary>
public sealed class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>,
      IDataProtectionKeyContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// </summary>
    /// <param name="options">The configured EF Core options for the current context instance.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the ASP.NET Core Data Protection keys stored in the shared database.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>
    /// Gets the application users set.
    /// </summary>
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();

    /// <summary>
    /// Gets the identity roles set.
    /// </summary>
    public DbSet<IdentityRole<Guid>> ApplicationRoles => Set<IdentityRole<Guid>>();

    /// <summary>
    /// Gets the workspaces set.
    /// </summary>
    public DbSet<Workspace> Workspaces => Set<Workspace>();

    /// <summary>
    /// Gets the workspace members set.
    /// </summary>
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    /// <summary>
    /// Gets the refresh tokens set.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Gets the connected social accounts set.
    /// </summary>
    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>();

    /// <summary>
    /// Gets the social account insights set.
    /// </summary>
    public DbSet<SocialAccountInsight> SocialAccountInsights => Set<SocialAccountInsight>();

    /// <summary>
    /// Gets the uploaded videos set.
    /// </summary>
    public DbSet<Video> Videos => Set<Video>();

    /// <summary>
    /// Gets the posts set.
    /// </summary>
    public DbSet<Post> Posts => Set<Post>();

    /// <summary>
    /// Gets the post targets set.
    /// </summary>
    public DbSet<PostTarget> PostTargets => Set<PostTarget>();

    /// <summary>
    /// Gets the publishing jobs set.
    /// </summary>
    public DbSet<PublishingJob> PublishingJobs => Set<PublishingJob>();

    /// <summary>
    /// Gets the post analytics snapshots set.
    /// </summary>
    public DbSet<PostAnalyticsSnapshot> PostAnalyticsSnapshots => Set<PostAnalyticsSnapshot>();

    /// <summary>
    /// Gets the inbox conversations set.
    /// </summary>
    public DbSet<InboxConversation> InboxConversations => Set<InboxConversation>();

    /// <summary>
    /// Gets the inbox messages set.
    /// </summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>
    /// Gets the conversation assignments set.
    /// </summary>
    public DbSet<ConversationAssignment> ConversationAssignments => Set<ConversationAssignment>();

    /// <summary>
    /// Gets the automation rules set.
    /// </summary>
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();

    /// <summary>
    /// Gets the trigger conditions set.
    /// </summary>
    public DbSet<TriggerCondition> TriggerConditions => Set<TriggerCondition>();

    /// <summary>
    /// Gets the automation actions set.
    /// </summary>
    public DbSet<AutomationAction> AutomationActions => Set<AutomationAction>();

    /// <summary>
    /// Gets the pending DM queue entries set.
    /// </summary>
    public DbSet<PendingDMQueue> PendingDMQueueEntries => Set<PendingDMQueue>();

    /// <summary>
    /// Gets the automation execution logs set.
    /// </summary>
    public DbSet<AutomationExecutionLog> AutomationExecutionLogs => Set<AutomationExecutionLog>();

    /// <summary>
    /// Gets the buffered webhook events set.
    /// </summary>
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    /// <summary>
    /// Gets the notification preferences set.
    /// </summary>
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    /// <summary>
    /// Gets the persisted notification delivery history set.
    /// </summary>
    public DbSet<NotificationHistory> NotificationHistories => Set<NotificationHistory>();

    /// <summary>
    /// Applies all entity type configurations from the Infrastructure assembly.
    /// </summary>
    /// <param name="builder">The EF Core model builder.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
