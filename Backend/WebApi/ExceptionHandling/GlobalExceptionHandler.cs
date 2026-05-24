// WebApi/ExceptionHandling/GlobalExceptionHandler.cs
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.ExceptionHandling;

/// <summary>
/// Global exception handler translating unhandled exceptions to standardized RFC 7807 ProblemDetails responses.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the <see cref="IExceptionHandler"/> interface to intercept, log, and translate raw system exceptions.</para>
/// <para><b>Business &amp; Technical Justification:</b> Essential for security and error tracking. Ensures the frontend receives predictable JSON errors.
/// TRD Security: "CWE-209 Prevention: Never leak internal Stack Traces, raw DB errors, or code structures to the API consumer in production."</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Automatically triggered during the request middleware pipeline whenever an exception is thrown in downstream layers (Domain, Application, Infrastructure).
/// Safely logs full diagnostic details locally, then transforms client-facing response payload to a safe standard.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Seals the presentation security boundary. Maximizes reliability by maintaining stable API contracts even during catastrophic database or third-party service crashes.</para>
/// </remarks>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>
    /// Initializes the global exception handler.
    /// </summary>
    /// <param name="problemDetailsService">The service used to write standardized ProblemDetails JSON payloads.</param>
    /// <param name="logger">The logger used to record diagnostic context on unhandled failures.</param>
    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Deep diagnostics: Log full stack trace safely inside server infrastructure logs.
        _logger.LogError(
            exception,
            "Unhandled exception occurred while processing {Method} {Path}.",
            httpContext.Request.Method,
            httpContext.Request.Path);

        // Security check & translation switch
        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed."),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Requested resource was not found."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected server error occurred.")
        };

        // Safety safeguard: Hide raw exception messages for 500 errors to prevent system reconnaissance (CWE-209).
        // Only return specific messages for known business exceptions (400, 403, 404).
        var detail = statusCode == StatusCodes.Status500InternalServerError
            ? "An unexpected server error occurred. Please contact system administrator."
            : exception.Message;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        // If it's a validation exception, populate RFC 7807 dynamic error collection
        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());
        }

        // Trace and temporal correlation parameters
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        problemDetails.Extensions["timestampUtc"] = DateTime.UtcNow;

        // Write the ProblemDetails JSON response to HTTP stream
        await _problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        return true;
    }
}

