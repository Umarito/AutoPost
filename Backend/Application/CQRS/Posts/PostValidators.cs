using FluentValidation;

namespace Application.CQRS.Posts;

/// <summary>
/// Validates post creation input.
/// </summary>
public sealed class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CreatePostCommand"/>.
    /// </summary>
    public CreatePostCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.VideoId).NotEmpty();
        RuleFor(x => x.Request.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Request.TargetAccountIds).NotNull().Must(ids => ids.Count > 0);
        RuleFor(x => x.Request.TimeZoneId).NotEmpty().MaximumLength(100);
    }
}

/// <summary>
/// Validates post update input.
/// </summary>
public sealed class UpdatePostCommandValidator : AbstractValidator<UpdatePostCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="UpdatePostCommand"/>.
    /// </summary>
    public UpdatePostCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Title).MaximumLength(500).When(x => x.Request.Title is not null);
        RuleFor(x => x.Request.Description).MaximumLength(5000).When(x => x.Request.Description is not null);
        RuleFor(x => x.Request.TimeZoneId).MaximumLength(100).When(x => x.Request.TimeZoneId is not null);
    }
}

/// <summary>
/// Validates commands that reference an existing post by identifier.
/// </summary>
public sealed class CancelPostCommandValidator : AbstractValidator<CancelPostCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CancelPostCommand"/>.
    /// </summary>
    public CancelPostCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
    }
}

/// <summary>
/// Validates delete post requests.
/// </summary>
public sealed class DeletePostCommandValidator : AbstractValidator<DeletePostCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="DeletePostCommand"/>.
    /// </summary>
    public DeletePostCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
    }
}

/// <summary>
/// Validates post rescheduling input.
/// </summary>
public sealed class ReschedulePostCommandValidator : AbstractValidator<ReschedulePostCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ReschedulePostCommand"/>.
    /// </summary>
    public ReschedulePostCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(100);
    }
}

/// <summary>
/// Validates explicit post publication requests.
/// </summary>
public sealed class PublishPostCommandValidator : AbstractValidator<PublishPostCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="PublishPostCommand"/>.
    /// </summary>
    public PublishPostCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
    }
}

/// <summary>
/// Validates target-level publication requests.
/// </summary>
public sealed class PublishToTargetCommandValidator : AbstractValidator<PublishToTargetCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="PublishToTargetCommand"/>.
    /// </summary>
    public PublishToTargetCommandValidator()
    {
        RuleFor(x => x.PostTargetId).NotEmpty();
    }
}

/// <summary>
/// Validates failed target retry requests.
/// </summary>
public sealed class RetryFailedTargetCommandValidator : AbstractValidator<RetryFailedTargetCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="RetryFailedTargetCommand"/>.
    /// </summary>
    public RetryFailedTargetCommandValidator()
    {
        RuleFor(x => x.PostTargetId).NotEmpty();
    }
}

/// <summary>
/// Validates publication result recording requests.
/// </summary>
public sealed class RecordPublishResultCommandValidator : AbstractValidator<RecordPublishResultCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="RecordPublishResultCommand"/>.
    /// </summary>
    public RecordPublishResultCommandValidator()
    {
        RuleFor(x => x.PostTargetId).NotEmpty();
    }
}

/// <summary>
/// Validates publishing job creation requests.
/// </summary>
public sealed class CreatePublishingJobCommandValidator : AbstractValidator<CreatePublishingJobCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CreatePublishingJobCommand"/>.
    /// </summary>
    public CreatePublishingJobCommandValidator()
    {
        RuleFor(x => x.PostTargetId).NotEmpty();
        RuleFor(x => x.AttemptNumber).GreaterThan(0);
    }
}

/// <summary>
/// Validates publishing job update requests.
/// </summary>
public sealed class UpdatePublishingJobCommandValidator : AbstractValidator<UpdatePublishingJobCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="UpdatePublishingJobCommand"/>.
    /// </summary>
    public UpdatePublishingJobCommandValidator()
    {
        RuleFor(x => x.PublishingJobId).NotEmpty();
        RuleFor(x => x.Outcome).NotEmpty();
    }
}

/// <summary>
/// Validates paged post list queries.
/// </summary>
public sealed class GetPostsPagedQueryValidator : AbstractValidator<GetPostsPagedQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetPostsPagedQuery"/>.
    /// </summary>
    public GetPostsPagedQueryValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

/// <summary>
/// Validates post detail queries.
/// </summary>
public sealed class GetPostByIdQueryValidator : AbstractValidator<GetPostByIdQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetPostByIdQuery"/>.
    /// </summary>
    public GetPostByIdQueryValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
    }
}

/// <summary>
/// Validates content calendar queries.
/// </summary>
public sealed class GetPostCalendarQueryValidator : AbstractValidator<GetPostCalendarQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetPostCalendarQuery"/>.
    /// </summary>
    public GetPostCalendarQueryValidator()
    {
        RuleFor(x => x.From).LessThanOrEqualTo(x => x.To);
    }
}

/// <summary>
/// Validates target status queries.
/// </summary>
public sealed class GetPostTargetStatusQueryValidator : AbstractValidator<GetPostTargetStatusQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetPostTargetStatusQuery"/>.
    /// </summary>
    public GetPostTargetStatusQueryValidator()
    {
        RuleFor(x => x.PostTargetId).NotEmpty();
    }
}

/// <summary>
/// Validates publishing history queries.
/// </summary>
public sealed class GetPublishingHistoryQueryValidator : AbstractValidator<GetPublishingHistoryQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetPublishingHistoryQuery"/>.
    /// </summary>
    public GetPublishingHistoryQueryValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

/// <summary>
/// Validates failed publication monitoring queries.
/// </summary>
public sealed class GetFailedPublicationsQueryValidator : AbstractValidator<GetFailedPublicationsQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetFailedPublicationsQuery"/>.
    /// </summary>
    public GetFailedPublicationsQueryValidator()
    {
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}
