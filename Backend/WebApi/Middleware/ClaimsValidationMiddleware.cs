// WebApi/Middleware/ClaimsValidationMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace WebApi.Middleware;

/// <summary>
/// Middleware to strictly validate JWT claims for authenticated requests before execution.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Intercepts incoming HTTP requests, inspecting claims for authenticated sessions to ensure safety.</para>
/// <para><b>Business &amp; Technical Justification:</b> Enforces strict tenant isolation by ensuring the sub claim (UserId) and workspace_id claim are present and parseable.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Executes right after the ASP.NET Core Authentication middleware in the pipeline, parsing Guid values.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Acts as an early security gateway. Returns clean ProblemDetails if required claims are missing or malformed, preventing IDOR leaks.</para>
/// </remarks>
public sealed class ClaimsValidationMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes the middleware.
    /// </summary>
    /// <param name="next">The next request delegate in the pipeline.</param>
    public ClaimsValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the claims validation filter.
    /// </summary>
    /// <param name="context">The active HttpContext.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var workspaceIdClaim = context.User.FindFirst("workspace_id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out _))
            {
                await WriteUnauthorizedResponseAsync(context, "Invalid or missing User identifier claim (sub).");
                return;
            }

            // Note: If the route is /api/workspace and the method is POST, the user might not have a workspace claim yet.
            // But for other protected routes, a workspace_id claim must be present.
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/api/workspace", StringComparison.OrdinalIgnoreCase) && 
                !path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(workspaceIdClaim) || !Guid.TryParse(workspaceIdClaim, out _))
                {
                    await WriteUnauthorizedResponseAsync(context, "Invalid or missing Workspace context claim.");
                    return;
                }
            }
        }

        await _next(context);
    }

    private static async Task WriteUnauthorizedResponseAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized Claims Context",
            Detail = message,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
    }
}
