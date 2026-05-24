// WebApi/Controllers/ApiControllerBase.cs
using Application.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.Controllers;

/// <summary>
/// Abstract base API controller providing shared MediatR mediator access and functional error mapping helpers.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Serves as the base controller class for all Web API routes, wrapping response mapping logic.</para>
/// <para><b>Business &amp; Technical Justification:</b> Avoids repetitive boilerplate error-handling blocks in individual endpoints. Integrates with MediatR to enforce strict separation of presentation and application layers.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Lazily resolves <see cref="IMediator"/> from the request-scoped dependency injection container. Maps the domain/application <see cref="Result"/> structure into the HTTP response stream.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Guarantees consistent HTTP response contracts and standardized RFC 7807 Problem Details formatting. Essential for secure API design by sanitizing error codes before exposing them to the network.</para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private IMediator? _mediator;

    /// <summary>
    /// Gets the shared MediatR pipeline sender instance resolved from HTTP context services.
    /// </summary>
    protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    /// <summary>
    /// Translates a non-generic <see cref="Result"/> to a standard <see cref="ActionResult"/>.
    /// </summary>
    /// <param name="result">The functional operation result wrapper.</param>
    /// <returns>A configured <see cref="ActionResult"/> with correct HTTP status codes.</returns>
    protected ActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok();
        }

        return MapError(result.Error, result.Code);
    }

    /// <summary>
    /// Translates a generic <see cref="Result{T}"/> containing a payload to a standard <see cref="ActionResult"/>.
    /// </summary>
    /// <typeparam name="T">The payload type returned on success.</typeparam>
    /// <param name="result">The functional operation result wrapper with payload.</param>
    /// <returns>A configured <see cref="ActionResult"/> containing the success payload or mapped HTTP errors.</returns>
    protected ActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            if (result.Value is null)
            {
                return NoContent();
            }
            return Ok(result.Value);
        }

        return MapError(result.Error, result.Code);
    }

    private ActionResult MapError(string? error, ErrorCode? code)
    {
        var message = error ?? "An unexpected processing error occurred.";
        
        return code switch
        {
            ErrorCode.NotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource Not Found",
                Detail = message,
                Instance = HttpContext.Request.Path
            }),
            ErrorCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Access Forbidden",
                Detail = message,
                Instance = HttpContext.Request.Path
            }),
            ErrorCode.Unauthorized => Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = message,
                Instance = HttpContext.Request.Path
            }),
            ErrorCode.Conflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict State Detected",
                Detail = message,
                Instance = HttpContext.Request.Path
            }),
            ErrorCode.Validation => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = message,
                Instance = HttpContext.Request.Path
            }),
            ErrorCode.ExternalApi => StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "Bad Gateway",
                Detail = "External API integration failed: " + message,
                Instance = HttpContext.Request.Path
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected server error occurred.",
                Instance = HttpContext.Request.Path
            })
        };
    }
}
