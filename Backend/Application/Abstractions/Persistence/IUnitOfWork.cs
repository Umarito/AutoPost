namespace Application.Abstractions.Persistence;

/// <summary>
/// Represents the application-facing persistence boundary for committing changes and opening transactions.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending tracked changes.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The number of state entries written to the underlying store.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begins a database transaction that spans multiple repository and identity operations.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>An opened transaction wrapper.</returns>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>
/// Represents an opened persistence transaction controlled by the Application layer.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A task that completes when the transaction is committed.</returns>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>
    /// Rolls the transaction back.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A task that completes when the transaction is rolled back.</returns>
    Task RollbackAsync(CancellationToken ct = default);
}
