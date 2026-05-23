using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Represents the tenant boundary for an organization or team.
/// </summary>
public class Workspace : BaseEntity<Guid>
{
    /// <summary>
    /// Gets the human-readable workspace name.
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Gets the globally unique URL slug of the workspace.
    /// </summary>
    public string Slug { get; private set; } = default!;

    /// <summary>
    /// Gets the optional branded logo URL.
    /// </summary>
    public string? LogoUrl { get; private set; }

    /// <summary>
    /// Gets the current subscription plan.
    /// </summary>
    public SubscriptionPlan Plan { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the paid subscription expires, or <c>null</c> for free plan.
    /// </summary>
    public DateTime? PlanExpiresAt { get; private set; }

    /// <summary>
    /// Gets the maximum number of active social accounts allowed by the plan.
    /// </summary>
    public int MaxSocialAccounts { get; private set; }

    /// <summary>
    /// Gets the maximum number of team members allowed by the plan.
    /// </summary>
    public int MaxTeamMembers { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the workspace was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the workspace is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the workspace members.
    /// </summary>
    public IReadOnlyCollection<WorkspaceMember> Members { get; private set; } = new List<WorkspaceMember>();

    /// <summary>
    /// Gets the connected social accounts.
    /// </summary>
    public IReadOnlyCollection<SocialAccount> SocialAccounts { get; private set; } = new List<SocialAccount>();

    /// <summary>
    /// Gets the posts owned by the workspace.
    /// </summary>
    public IReadOnlyCollection<Post> Posts { get; private set; } = new List<Post>();

    /// <summary>
    /// Gets the automation rules owned by the workspace.
    /// </summary>
    public IReadOnlyCollection<AutomationRule> AutomationRules { get; private set; } = new List<AutomationRule>();

    /// <summary>
    /// Creates a new workspace using plan defaults defined by the MVP business rules.
    /// </summary>
    /// <param name="name">Display name of the workspace.</param>
    /// <param name="slug">URL-safe unique slug.</param>
    /// <param name="createdAtUtc">UTC creation timestamp.</param>
    /// <param name="plan">Plan to assign during creation.</param>
    /// <returns>A fully initialized <see cref="Workspace"/> entity.</returns>
    public static Workspace Create(
        string name,
        string slug,
        DateTime createdAtUtc,
        SubscriptionPlan plan = SubscriptionPlan.Free)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var limits = ResolvePlanLimits(plan);

        return new Workspace
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim(),
            Plan = plan,
            PlanExpiresAt = null,
            MaxSocialAccounts = limits.MaxSocialAccounts,
            MaxTeamMembers = limits.MaxTeamMembers,
            CreatedAt = createdAtUtc,
            IsActive = true
        };
    }

    /// <summary>
    /// Updates branding fields using patch semantics.
    /// </summary>
    /// <param name="name">New name, or <c>null</c> to keep the current value.</param>
    /// <param name="logoUrl">New logo URL, or <c>null</c> to keep the current value.</param>
    public void UpdateBranding(string? name, string? logoUrl)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (logoUrl is not null)
        {
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        }
    }

    /// <summary>
    /// Deactivates the workspace while preserving historical data.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Applies a new subscription plan and updates the corresponding limits.
    /// </summary>
    /// <param name="plan">New subscription plan.</param>
    /// <param name="planExpiresAtUtc">UTC expiration timestamp for the paid plan, if any.</param>
    public void ChangePlan(SubscriptionPlan plan, DateTime? planExpiresAtUtc)
    {
        var limits = ResolvePlanLimits(plan);
        Plan = plan;
        PlanExpiresAt = planExpiresAtUtc;
        MaxSocialAccounts = limits.MaxSocialAccounts;
        MaxTeamMembers = limits.MaxTeamMembers;
    }

    private static (int MaxSocialAccounts, int MaxTeamMembers) ResolvePlanLimits(SubscriptionPlan plan)
    {
        return plan switch
        {
            SubscriptionPlan.Free => (2, 1),
            SubscriptionPlan.Pro => (10, 5),
            SubscriptionPlan.Business => (25, 25),
            SubscriptionPlan.Enterprise => (int.MaxValue, int.MaxValue),
            _ => (2, 1)
        };
    }
}
