using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs the name and execution duration of every request.
///
/// <para><b>What:</b>
/// An implementation of <see cref="IPipelineBehavior{TRequest,TResponse}"/> that wraps
/// every MediatR request with structured log entries showing the request name, execution
/// time in milliseconds, and whether it succeeded or failed.</para>
///
/// <para><b>Why:</b>
/// Provides automatic observability for all CQRS operations without modifying individual
/// handlers. This is essential for performance monitoring, debugging slow queries, and
/// identifying bottlenecks in production. Following the Open/Closed Principle — we add
/// logging behavior without touching existing handler code.</para>
///
/// <para><b>Configuration:</b>
/// Registered as an open generic in the DI container via
/// <c>AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;))</c>.
/// Log output goes through <see cref="ILogger"/> which is configured at the host level
/// (Console, File, Seq, Application Insights, etc.).</para>
///
/// <para><b>Performance note:</b>
/// Requests taking longer than 500ms are logged at Warning level to help identify
/// slow operations in production monitoring dashboards.</para>
/// </summary>
/// <typeparam name="TRequest">The MediatR request type (command or query).</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes the behavior with a typed logger for structured logging output.
    /// </summary>
    /// <param name="logger">Logger instance scoped to this behavior's generic type.</param>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs the start of request handling, measures execution time, and logs the result.
    /// Exceptions are logged at Error level and then re-thrown to preserve the original
    /// exception flow for the global exception handler.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("→ Handling {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);

            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;

            // Warn on slow operations (> 500ms) to surface potential performance issues.
            if (elapsed > 500)
            {
                _logger.LogWarning(
                    "⚠ {RequestName} completed in {ElapsedMs}ms (slow)",
                    requestName, elapsed);
            }
            else
            {
                _logger.LogInformation(
                    "← Handled {RequestName} in {ElapsedMs}ms",
                    requestName, elapsed);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "✗ {RequestName} failed after {ElapsedMs}ms — {ErrorMessage}",
                requestName, stopwatch.ElapsedMilliseconds, ex.Message);

            throw; // Re-throw to let the global exception handler deal with it.
        }
    }
}
