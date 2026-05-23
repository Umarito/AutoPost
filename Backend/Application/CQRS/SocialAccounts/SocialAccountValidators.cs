using FluentValidation;

namespace Application.CQRS.SocialAccounts;

/// <summary>
/// Validates OAuth callback processing input.
/// </summary>
public sealed class HandleOAuthCallbackCommandValidator : AbstractValidator<HandleOAuthCallbackCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="HandleOAuthCallbackCommand"/>.
    /// </summary>
    public HandleOAuthCallbackCommandValidator()
    {
        RuleFor(x => x.AuthorizationCode).NotEmpty();
        RuleFor(x => x.State).NotEmpty();
        RuleFor(x => x.RedirectUri).NotEmpty();
    }
}

/// <summary>
/// Validates social account disconnection input.
/// </summary>
public sealed class DisconnectSocialAccountCommandValidator : AbstractValidator<DisconnectSocialAccountCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="DisconnectSocialAccountCommand"/>.
    /// </summary>
    public DisconnectSocialAccountCommandValidator()
    {
        RuleFor(x => x.SocialAccountId).NotEmpty();
    }
}

/// <summary>
/// Validates explicit token refresh requests.
/// </summary>
public sealed class EnsureTokenValidCommandValidator : AbstractValidator<EnsureTokenValidCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="EnsureTokenValidCommand"/>.
    /// </summary>
    public EnsureTokenValidCommandValidator()
    {
        RuleFor(x => x.SocialAccountId).NotEmpty();
    }
}

/// <summary>
/// Validates metadata refresh requests for connected social accounts.
/// </summary>
public sealed class RefreshSocialAccountMetaCommandValidator : AbstractValidator<RefreshSocialAccountMetaCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="RefreshSocialAccountMetaCommand"/>.
    /// </summary>
    public RefreshSocialAccountMetaCommandValidator()
    {
        RuleFor(x => x.SocialAccountId).NotEmpty();
    }
}

/// <summary>
/// Validates insight collection requests.
/// </summary>
public sealed class CollectAccountInsightCommandValidator : AbstractValidator<CollectAccountInsightCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CollectAccountInsightCommand"/>.
    /// </summary>
    public CollectAccountInsightCommandValidator()
    {
        RuleFor(x => x.SocialAccountId).NotEmpty();
    }
}

/// <summary>
/// Validates OAuth URL generation requests.
/// </summary>
public sealed class GetOAuthUrlQueryValidator : AbstractValidator<GetOAuthUrlQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetOAuthUrlQuery"/>.
    /// </summary>
    public GetOAuthUrlQueryValidator()
    {
        RuleFor(x => x.RedirectUri).NotEmpty();
    }
}

/// <summary>
/// Validates growth history queries.
/// </summary>
public sealed class GetAccountGrowthQueryValidator : AbstractValidator<GetAccountGrowthQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetAccountGrowthQuery"/>.
    /// </summary>
    public GetAccountGrowthQueryValidator()
    {
        RuleFor(x => x.SocialAccountId).NotEmpty();
        RuleFor(x => x.From).LessThanOrEqualTo(x => x.To);
    }
}
