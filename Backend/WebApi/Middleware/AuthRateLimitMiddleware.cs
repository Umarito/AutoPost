using Application.Abstractions.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using WebApi.Options;

namespace WebApi.Middleware;

/// <summary>
/// Applies Redis-backed brute-force protection to authentication endpoints.
/// </summary>
public sealed class AuthRateLimitMiddleware
{
    private static readonly PathString AuthPrefix = new("/api/auth");

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes the middleware with the next pipeline delegate.
    /// </summary>
    /// <param name="next">The next middleware in the request pipeline.</param>
    public AuthRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Enforces a distributed auth rate limit before the request reaches authentication handlers.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="redisRateLimitService">The distributed Redis-backed rate-limit service.</param>
    /// <param name="rateLimitOptions">The configured rate-limit thresholds.</param>
    /// <returns>A task representing the middleware execution.</returns>
    public async Task InvokeAsync(
        HttpContext context,
        IRedisRateLimitService redisRateLimitService,
        IOptions<RateLimitOptions> rateLimitOptions)
    {
        if (!context.Request.Path.StartsWithSegments(AuthPrefix))
        {
            await _next(context);
            return;
        }

        var options = rateLimitOptions.Value;
        var clientKey = BuildClientKey(context);
        var decision = await redisRateLimitService.ConsumeAsync(
            scope: "auth",
            key: clientKey,
            permitLimit: options.AuthPermitLimit,
            window: TimeSpan.FromSeconds(options.AuthWindowSeconds),
            context.RequestAborted);

        if (decision.IsAllowed)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many authentication attempts.",
            Detail = "Please wait before retrying the authentication request.",
            Instance = context.Request.Path
        };

        problem.Extensions["retryAfterSeconds"] = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
    }

    private static string BuildClientKey(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var composite = $"{ip}:{userAgent}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(composite));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
