// WebApi/Controllers/AuthController.cs
using Application.Common;
using Application.CQRS.Auth;
using Application.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebApi.Options;
using WebApi.Security;
using WebApi.Seeds;

namespace WebApi.Controllers;

/// <summary>
/// Manages user authentication, session state, token refreshes, and profile updates.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the Web API endpoints for the authentication module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Provides entry points for user onboarding and secure session establishment. Enforces HttpOnly cookies for refresh tokens to defend against Cross-Site Scripting (XSS) attacks.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Communicates directly with the Application layer using MediatR. Interacts with <see cref="IRefreshTokenCookieService"/> to manage client-side state.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Seals the authentication boundaries of the system. Implements brute-force protection hooks and secure session cleanup endpoints.</para>
/// </remarks>
public sealed class AuthController : ApiControllerBase
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IRefreshTokenCookieService _cookieService;
    private readonly RefreshTokenOptions _refreshTokenOptions;

    /// <summary>
    /// Initializes the AuthController.
    /// </summary>
    /// <param name="currentUserContext">Access to current user and session identifiers from claims.</param>
    /// <param name="cookieService">Service to append/delete HTTP cookies containing refresh tokens.</param>
    /// <param name="refreshTokenOptions">The settings for refresh token lifetimes and cookie attributes.</param>
    public AuthController(
        ICurrentUserContext currentUserContext,
        IRefreshTokenCookieService cookieService,
        IOptions<RefreshTokenOptions> refreshTokenOptions)
    {
        _currentUserContext = currentUserContext;
        _cookieService = cookieService;
        _refreshTokenOptions = refreshTokenOptions.Value;
    }

    /// <summary>
    /// Registers a new user account and provisions their first workspace context.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/auth/register - User onboarding route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Essential for the MVP self-service signup. Initializes the default tenant workspace automatically.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Accepts validation parameters, routes payload via <see cref="RegisterUserCommand"/>, issues JWT + Refresh tokens, and dispatches a confirmation email.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Restricts spam registrations via Redis rate limits (5 per IP window). Binds refresh tokens to secure cookies.</para>
    /// </remarks>
    /// <param name="request">Payload containing username, email, and password credentials.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>User profile data and active session tokens.</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthTokensDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var command = new RegisterUserCommand(request);
        var result = await Mediator.Send(command, ct);

        if (result.IsSuccess && result.Value is not null)
        {
            SetRefreshTokenCookie(result.Value.RefreshToken);
        }

        return HandleResult(result);
    }

    /// <summary>
    /// Authenticates credentials and issues API session tokens.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/auth/login - Main credential authentication route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Verifies user identity before giving access to publishing dashboards.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Processes credentials via Identity UserManager, resolves primary workspace membership, generates tokens, and updates cache states.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Implements brute-force rate limits. Ensures refresh token hashes are securely stored in database repositories.</para>
    /// </remarks>
    /// <param name="request">Payload containing registered email and plaintext password.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Auth token metadata containing access token and expiration.</returns>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthTokensDto))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request);
        var result = await Mediator.Send(command, ct);

        if (result.IsSuccess && result.Value is not null)
        {
            SetRefreshTokenCookie(result.Value.RefreshToken);
        }

        return HandleResult(result);
    }

    /// <summary>
    /// Exchanges an opaque refresh token cookie for a rotated access and refresh token pair.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/auth/refresh - Token rotation and session continuation route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows users to remain logged in safely without long-lived JWT access tokens.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Pulls the token from HttpOnly cookies, triggers <see cref="RefreshTokenCommand"/>, verifies cryptohash matching, and sets the updated cookie.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Crucial for token replay and theft detection. If a reused token is detected, all user sessions are immediately revoked.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The new access token and its expiration.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthTokensDto))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Refresh(CancellationToken ct)
    {
        var token = Request.Cookies[_refreshTokenOptions.CookieName];
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Missing Refresh Token",
                Detail = "No refresh token cookie was supplied."
            });
        }

        var command = new RefreshTokenCommand(token);
        var result = await Mediator.Send(command, ct);

        if (result.IsSuccess && result.Value is not null)
        {
            SetRefreshTokenCookie(result.Value.RefreshToken);
        }
        else
        {
            _cookieService.DeleteRefreshToken(Response);
        }

        return HandleResult(result);
    }

    /// <summary>
    /// Terminate the current user session and revoke the corresponding refresh token.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/auth/logout - Session termination.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Standard security best practice to ensure session context is fully destroyed on the server.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Deletes local cookie, dispatches <see cref="LogoutCommand"/> to revoke DB token state, and purges Redis user caches.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seals security state. Logout is fully idempotent to prevent timing attacks or enumeration.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>HTTP 200 OK status on success.</returns>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Logout(CancellationToken ct)
    {
        var token = Request.Cookies[_refreshTokenOptions.CookieName];
        if (!string.IsNullOrWhiteSpace(token))
        {
            var command = new LogoutCommand(token);
            await Mediator.Send(command, ct);
        }

        _cookieService.DeleteRefreshToken(Response);
        return Ok();
    }

    /// <summary>
    /// Revokes all active sessions and sessions for the current user across all devices.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/auth/logout-all - Mass session revocation.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Essential security mechanism when a user suspects account compromise or changes their password.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Requires authentication. Dispatches <see cref="LogoutAllDevicesCommand"/>, wipes all DB refresh tokens for user, and purges cookie.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Highly effective account lockdown safeguard. Protects against persistent token reuse across devices.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> LogoutAll(CancellationToken ct)
    {
        var command = new LogoutAllDevicesCommand();
        var result = await Mediator.Send(command, ct);
        
        _cookieService.DeleteRefreshToken(Response);
        return HandleResult(result);
    }

    /// <summary>
    /// Confirms the user's email address using the registration verification token.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/auth/confirm-email - Verification callback.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Essential for spam mitigation and validation of user contact details.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Takes user GUID and token string, invokes <see cref="ConfirmEmailCommand"/>, and returns verification status.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Guards user activation boundaries. Ensures email-unconfirmed restrictions can be applied in downstream services.</para>
    /// </remarks>
    /// <param name="userId">The unique identifier of the registering user.</param>
    /// <param name="token">Email verification token.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpGet("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token, CancellationToken ct)
    {
        var command = new ConfirmEmailCommand(userId, token);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Re-sends the registration verification confirmation message to the user email.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/auth/resend-confirmation - User activation retry route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Handles scenarios where verification emails are lost, blocked or expired.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Receives email, invokes <see cref="ResendEmailConfirmationCommand"/>, checks antispam rate limits, and enqueues a new confirmation email.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Strictly rate-limited (3 attempts per hour per email) to protect internal mail queues from abuse.</para>
    /// </remarks>
    /// <param name="email">Email of the user requiring a new confirmation token.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("resend-confirmation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> ResendConfirmation([FromQuery] string email, CancellationToken ct)
    {
        var command = new ResendEmailConfirmationCommand(email);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves the profile details of the current authenticated user.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/auth/profile - Personal profile retrieve route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Provides the frontend dashboard with details for rendering profile photos and timezone configurations.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries user data based on validated JWT subject, executing <see cref="GetUserByIdQuery"/>.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Blocks IDOR entirely. Cross-checks requested GUID with token claims to prevent browsing external records.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The authenticated user's profile details.</returns>
    [Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserProfileDto))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetProfile(CancellationToken ct)
    {
        var query = new GetUserByIdQuery(_currentUserContext.UserId);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Updates the editable profile fields of the current authenticated user.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> PUT /api/auth/profile - Profile updates route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows users to customize display names, avatars, and regional time zones.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Accept payload, updates user entity properties, triggers unit-of-work save, and logs modifications.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Restricts mass assignment vulnerabilities by binding parameters to explicit requests. Sanitizes timezones.</para>
    /// </remarks>
    /// <param name="request">Payload containing timezone and profile settings.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The updated user profile details.</returns>
    [Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserProfileDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var command = new UpdateUserProfileCommand(request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves all active and historic sessions associated with the current user.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/auth/sessions - Sessions list.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Provides transparent session management for the user to review logged-in device locations.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Executes <see cref="GetUserSessionsQuery"/>, queries database, and caches output to Redis to minimize database lookups.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Enhances system auditability and transparency. Prevents user ID enumeration via claims containment.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A collection of active and historical sessions.</returns>
    [Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
    [HttpGet("sessions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<UserSessionDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetSessions(CancellationToken ct)
    {
        var query = new GetUserSessionsQuery();
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves only active sessions associated with the current user.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/auth/sessions/active - Active sessions list.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Optimizes lists for active logins and authorization validation views.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Executes <see cref="GetActiveSessionsQuery"/>, pulls active sessions, and caches results to Redis.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Reduces system scale lookup overhead. Protects system privacy boundaries.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A collection of active sessions.</returns>
    [Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
    [HttpGet("sessions/active")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<UserSessionDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetActiveSessions(CancellationToken ct)
    {
        var query = new GetActiveSessionsQuery();
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Revokes a specific session associated with the user by its unique refresh token identifier.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/auth/sessions/{id}/revoke - Session revocation.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows targeted logging-out of separate devices without resetting the primary session.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Dispatches <see cref="RevokeRefreshTokenCommand"/>, locates target token, checks ownership claims, updates database state, and purges Redis cache.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Guards against IDOR: users are blocked from revoking tokens that belong to other users via matching logic.</para>
    /// </remarks>
    /// <param name="id">The unique identifier of the session token to revoke.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
    [HttpPost("sessions/{id:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> RevokeSession(Guid id, CancellationToken ct)
    {
        var command = new RevokeRefreshTokenCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var expiresAtUtc = DateTime.UtcNow.AddDays(_refreshTokenOptions.LifetimeDays);
        _cookieService.AppendRefreshToken(Response, refreshToken, expiresAtUtc);
    }
}
