using FluentValidation;

namespace Application.CQRS.Auth;

/// <summary>
/// Validates registration command input before any identity, workspace or token work is performed.
/// </summary>
public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="RegisterUserCommand"/>.
    /// </summary>
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Request.DisplayName).NotEmpty().MaximumLength(200);
    }
}

/// <summary>
/// Validates login command input before authentication and rate-limit checks run.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="LoginCommand"/>.
    /// </summary>
    public LoginCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.Password).NotEmpty();
    }
}

/// <summary>
/// Validates refresh token exchange input.
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="RefreshTokenCommand"/>.
    /// </summary>
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

/// <summary>
/// Validates logout input for the current session.
/// </summary>
public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="LogoutCommand"/>.
    /// </summary>
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

/// <summary>
/// Validates email confirmation requests before the identity token is consumed.
/// </summary>
public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ConfirmEmailCommand"/>.
    /// </summary>
    public ConfirmEmailCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
    }
}

/// <summary>
/// Validates requests that re-send the email confirmation message.
/// </summary>
public sealed class ResendEmailConfirmationCommandValidator : AbstractValidator<ResendEmailConfirmationCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ResendEmailConfirmationCommand"/>.
    /// </summary>
    public ResendEmailConfirmationCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

/// <summary>
/// Validates user profile update requests.
/// </summary>
public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="UpdateUserProfileCommand"/>.
    /// </summary>
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.DisplayName).MaximumLength(200).When(x => x.Request.DisplayName is not null);
        RuleFor(x => x.Request.AvatarUrl).MaximumLength(2048).When(x => x.Request.AvatarUrl is not null);
        RuleFor(x => x.Request.TimeZoneId).MaximumLength(100).When(x => x.Request.TimeZoneId is not null);
        RuleFor(x => x.Request.Locale).MaximumLength(10).When(x => x.Request.Locale is not null);
    }
}

/// <summary>
/// Validates explicit refresh token rotation requests.
/// </summary>
public sealed class RotateRefreshTokenCommandValidator : AbstractValidator<RotateRefreshTokenCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="RotateRefreshTokenCommand"/>.
    /// </summary>
    public RotateRefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

/// <summary>
/// Validates specific-session revocation commands.
/// </summary>
public sealed class RevokeRefreshTokenCommandValidator : AbstractValidator<RevokeRefreshTokenCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="RevokeRefreshTokenCommand"/>.
    /// </summary>
    public RevokeRefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshTokenId).NotEmpty();
    }
}

/// <summary>
/// Validates profile lookup queries.
/// </summary>
public sealed class GetUserByIdQueryValidator : AbstractValidator<GetUserByIdQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetUserByIdQuery"/>.
    /// </summary>
    public GetUserByIdQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
