using FluentValidation;
using MediatR;

namespace Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that intercepts every request and runs all registered
/// FluentValidation validators before the handler executes.
///
/// <para><b>What:</b>
/// An implementation of <see cref="IPipelineBehavior{TRequest,TResponse}"/> that acts
/// as middleware in the MediatR pipeline. It collects all <see cref="IValidator{TRequest}"/>
/// instances from the DI container and validates the incoming request.</para>
///
/// <para><b>Why:</b>
/// Centralizes validation logic so that individual handlers don't need to perform
/// their own validation. This enforces the Single Responsibility Principle — handlers
/// focus on business logic, while validation is a cross-cutting concern handled here.</para>
///
/// <para><b>How:</b>
/// 1. DI injects all validators for the request type.
/// 2. Each validator runs against the request.
/// 3. If any validation errors are found, a <see cref="ValidationException"/> is thrown
///    BEFORE the handler executes — preventing invalid data from reaching business logic.
/// 4. If no errors, the request passes through to the next behavior or handler.</para>
/// </summary>
/// <typeparam name="TRequest">The MediatR request type (command or query).</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// Initializes the behavior with all validators registered for <typeparamref name="TRequest"/>.
    /// If no validators exist for the request type, the collection will be empty and the
    /// request passes through without validation.
    /// </summary>
    /// <param name="validators">
    /// All FluentValidation validators for the request type, injected by the DI container.
    /// Empty if no validators are registered — in that case, validation is skipped.
    /// </param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Intercepts the request, runs all validators, and either throws on failure
    /// or passes the request to the next delegate in the pipeline.
    /// </summary>
    /// <param name="request">The incoming MediatR request.</param>
    /// <param name="next">Delegate to the next behavior or the final handler.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The response from the handler if validation passes.</returns>
    /// <exception cref="ValidationException">
    /// Thrown when one or more validators report errors. Contains all validation failures
    /// so the caller can display them (e.g., as a 400 Bad Request with error details).
    /// </exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // If no validators are registered for this request type, skip validation entirely.
        if (!_validators.Any())
            return await next(cancellationToken);

        // Run all validators in parallel and collect results.
        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Aggregate all failures from all validators into a single list.
        var failures = validationResults
            .Where(r => r.Errors.Count > 0)
            .SelectMany(r => r.Errors)
            .ToList();

        // If there are failures, throw a ValidationException — this will be caught
        // by the global exception handler middleware and returned as 400 Bad Request.
        if (failures.Count > 0)
            throw new ValidationException(failures);

        // Validation passed — proceed to the next behavior or handler.
        return await next(cancellationToken);
    }
}
