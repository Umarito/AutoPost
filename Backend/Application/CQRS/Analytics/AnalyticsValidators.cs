using FluentValidation;

namespace Application.CQRS.Analytics;

/// <summary>
/// Validates analytics snapshot collection requests.
/// </summary>
public sealed class CollectPostSnapshotCommandValidator : AbstractValidator<CollectPostSnapshotCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CollectPostSnapshotCommand"/>.
    /// </summary>
    public CollectPostSnapshotCommandValidator()
    {
        RuleFor(x => x.PostTargetId).NotEmpty();
    }
}

/// <summary>
/// Validates post analytics queries.
/// </summary>
public sealed class GetPostAnalyticsQueryValidator : AbstractValidator<GetPostAnalyticsQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetPostAnalyticsQuery"/>.
    /// </summary>
    public GetPostAnalyticsQueryValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
    }
}

/// <summary>
/// Validates dashboard summary queries.
/// </summary>
public sealed class GetDashboardSummaryQueryValidator : AbstractValidator<GetDashboardSummaryQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetDashboardSummaryQuery"/>.
    /// </summary>
    public GetDashboardSummaryQueryValidator()
    {
        RuleFor(x => x.From)
            .LessThanOrEqualTo(x => x.To!.Value)
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}
