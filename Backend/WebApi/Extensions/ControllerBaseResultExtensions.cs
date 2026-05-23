using Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Extensions;

/// <summary>
/// Converts application <see cref="Result{T}"/> values into HTTP action results.
/// </summary>
public static class ControllerBaseResultExtensions
{
    /// <summary>
    /// Maps an application result into a conventional MVC action result.
    /// </summary>
    /// <typeparam name="T">The payload type carried by the application result.</typeparam>
    /// <param name="controller">The controller that is returning the result.</param>
    /// <param name="result">The application result produced by a service or handler.</param>
    /// <returns>An HTTP action result with the appropriate status code and payload.</returns>
    public static ActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return result.Code switch
        {
            ErrorCode.NotFound => controller.NotFound(new ProblemDetails
            {
                Title = "Resource not found.",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            }),
            ErrorCode.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Access denied.",
                Detail = result.Error,
                Status = StatusCodes.Status403Forbidden
            }),
            ErrorCode.Conflict => controller.Conflict(new ProblemDetails
            {
                Title = "Conflict.",
                Detail = result.Error,
                Status = StatusCodes.Status409Conflict
            }),
            ErrorCode.Validation => controller.BadRequest(new ProblemDetails
            {
                Title = "Validation failed.",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            }),
            ErrorCode.Unauthorized => controller.Unauthorized(new ProblemDetails
            {
                Title = "Authentication required.",
                Detail = result.Error,
                Status = StatusCodes.Status401Unauthorized
            }),
            ErrorCode.ExternalApi => controller.StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "External platform error.",
                Detail = result.Error,
                Status = StatusCodes.Status502BadGateway
            }),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Unexpected server error.",
                Detail = result.Error,
                Status = StatusCodes.Status500InternalServerError
            })
        };
    }
}
