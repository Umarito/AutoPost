using FluentValidation;

namespace Application.CQRS.Inbox;

/// <summary>
/// Validates conversation upsert input sourced from webhook or polling flows.
/// </summary>
public sealed class CreateOrUpdateConversationCommandValidator : AbstractValidator<CreateOrUpdateConversationCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CreateOrUpdateConversationCommand"/>.
    /// </summary>
    public CreateOrUpdateConversationCommandValidator()
    {
        RuleFor(x => x.SocialAccountId).NotEmpty();
        RuleFor(x => x.ExternalConversationId).NotEmpty();
        RuleFor(x => x.Platform).NotEmpty();
        RuleFor(x => x.ExternalUserId).NotEmpty();
        RuleFor(x => x.ExternalUserName).NotEmpty();
        RuleFor(x => x.Type).NotEmpty();
    }
}

/// <summary>
/// Validates conversation status change requests.
/// </summary>
public sealed class ChangeConversationStatusCommandValidator : AbstractValidator<ChangeConversationStatusCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ChangeConversationStatusCommand"/>.
    /// </summary>
    public ChangeConversationStatusCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
    }
}

/// <summary>
/// Validates inbox assignment requests.
/// </summary>
public sealed class AssignConversationCommandValidator : AbstractValidator<AssignConversationCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="AssignConversationCommand"/>.
    /// </summary>
    public AssignConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Note).MaximumLength(500).When(x => x.Request.Note is not null);
    }
}

/// <summary>
/// Validates read-acknowledgement requests.
/// </summary>
public sealed class MarkConversationReadCommandValidator : AbstractValidator<MarkConversationReadCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="MarkConversationReadCommand"/>.
    /// </summary>
    public MarkConversationReadCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
    }
}

/// <summary>
/// Validates outbound message send requests.
/// </summary>
public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="SendMessageCommand"/>.
    /// </summary>
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.TextContent).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.Request.ReplyToMessageId).NotEqual(Guid.Empty).When(x => x.Request.ReplyToMessageId.HasValue);
    }
}

/// <summary>
/// Validates message delivery update requests.
/// </summary>
public sealed class UpdateMessageDeliveryStatusCommandValidator : AbstractValidator<UpdateMessageDeliveryStatusCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="UpdateMessageDeliveryStatusCommand"/>.
    /// </summary>
    public UpdateMessageDeliveryStatusCommandValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
    }
}

/// <summary>
/// Validates message deletion requests.
/// </summary>
public sealed class DeleteMessageCommandValidator : AbstractValidator<DeleteMessageCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="DeleteMessageCommand"/>.
    /// </summary>
    public DeleteMessageCommandValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
    }
}

/// <summary>
/// Validates paginated conversation list queries.
/// </summary>
public sealed class GetConversationsPagedQueryValidator : AbstractValidator<GetConversationsPagedQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetConversationsPagedQuery"/>.
    /// </summary>
    public GetConversationsPagedQueryValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

/// <summary>
/// Validates conversation detail queries.
/// </summary>
public sealed class GetConversationDetailQueryValidator : AbstractValidator<GetConversationDetailQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetConversationDetailQuery"/>.
    /// </summary>
    public GetConversationDetailQueryValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
    }
}

/// <summary>
/// Validates inbox search queries.
/// </summary>
public sealed class SearchConversationsQueryValidator : AbstractValidator<SearchConversationsQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="SearchConversationsQuery"/>.
    /// </summary>
    public SearchConversationsQueryValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter.Search).NotEmpty();
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

/// <summary>
/// Validates message list queries.
/// </summary>
public sealed class GetMessagesPagedQueryValidator : AbstractValidator<GetMessagesPagedQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetMessagesPagedQuery"/>.
    /// </summary>
    public GetMessagesPagedQueryValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}
