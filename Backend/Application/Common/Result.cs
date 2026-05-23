namespace Application.Common;

/// <summary>
/// Non-generic functional result wrapper used by commands that either succeed without
/// returning a payload or where the payload is not required by the caller.
///
/// <para><b>Why it exists:</b>
/// Many CQRS commands in AutoPost perform state transitions, schedule background work,
/// or trigger outbound integrations without needing to return a DTO. Returning
/// <see cref="Result"/> keeps those handlers explicit and avoids exception-driven
/// business flow for expected outcomes such as validation, conflict, authorization
/// and external API failures.</para>
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the operation completed successfully.
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Human-readable error message, available only when <see cref="IsSuccess"/> is <c>false</c>.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Machine-readable error classification used by the Web API layer to translate
    /// the result into the correct HTTP status code and RFC 7807 problem details response.
    /// </summary>
    public ErrorCode? Code { get; private set; }

    /// <summary>
    /// Creates a successful non-generic result.
    /// </summary>
    /// <returns>A successful <see cref="Result"/> instance.</returns>
    public static Result Ok() => new() { IsSuccess = true };

    /// <summary>
    /// Creates a failed non-generic result with a message and error code.
    /// </summary>
    /// <param name="error">Human-readable description of the failure.</param>
    /// <param name="code">Machine-readable failure category.</param>
    /// <returns>A failed <see cref="Result"/> instance.</returns>
    public static Result Fail(string error, ErrorCode code) => new() { Error = error, Code = code };
}

/// <summary>
/// Functional result wrapper that replaces exception-based flow for business operations.
///
/// <para><b>Why it exists:</b>
/// Business outcomes like duplicate email, revoked token, insufficient permissions
/// or platform rejection are expected states, not exceptional conditions. This
/// wrapper makes those outcomes explicit and easy to map to HTTP responses.</para>
/// </summary>
/// <typeparam name="T">The payload type returned on success.</typeparam>
public class Result<T>
{
    /// <summary>
    /// Indicates whether the operation completed successfully.
    /// When <c>true</c>, <see cref="Value"/> contains the payload.
    /// When <c>false</c>, <see cref="Error"/> and <see cref="Code"/> describe the failure.
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Payload returned by a successful operation.
    /// </summary>
    public T? Value { get; private set; }

    /// <summary>
    /// Human-readable error message, available only when <see cref="IsSuccess"/> is <c>false</c>.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Machine-readable error category used for HTTP status mapping.
    /// </summary>
    public ErrorCode? Code { get; private set; }

    /// <summary>
    /// Creates a successful result with a payload.
    /// </summary>
    /// <param name="value">The payload to return to the caller.</param>
    /// <returns>A successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Ok(T value) => new() { IsSuccess = true, Value = value };

    /// <summary>
    /// Creates a failed result with a message and error code.
    /// </summary>
    /// <param name="error">Human-readable description of the failure.</param>
    /// <param name="code">Machine-readable failure category.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static Result<T> Fail(string error, ErrorCode code) => new() { Error = error, Code = code };
}

/// <summary>
/// Machine-readable error classification for mapping business failures to HTTP semantics.
/// </summary>
public enum ErrorCode
{
    /// <summary>
    /// The requested entity does not exist inside the current tenant boundary.
    /// </summary>
    NotFound,

    /// <summary>
    /// The current user is authenticated but does not have permission to perform the action.
    /// </summary>
    Forbidden,

    /// <summary>
    /// The operation conflicts with existing state such as uniqueness rules or invalid status transitions.
    /// </summary>
    Conflict,

    /// <summary>
    /// The supplied input failed business or syntactic validation.
    /// </summary>
    Validation,

    /// <summary>
    /// A required external platform returned an error or rejected the request.
    /// </summary>
    ExternalApi,

    /// <summary>
    /// The caller is not authenticated or the provided credentials are invalid.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// An unexpected failure occurred that does not match a more specific category.
    /// </summary>
    Unknown
}
