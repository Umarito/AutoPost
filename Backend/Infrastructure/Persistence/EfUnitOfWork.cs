using Application.Abstractions.Persistence;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence;

/// <summary>
/// Exposes EF Core transaction and save semantics through application-facing abstractions.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes the unit of work wrapper.
    /// </summary>
    /// <param name="dbContext">The shared application database context.</param>
    public EfUnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        return new EfUnitOfWorkTransaction(transaction);
    }

    /// <summary>
    /// Bridges EF Core transactions to the application transaction abstraction.
    /// </summary>
    private sealed class EfUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        /// <summary>
        /// Initializes the transaction wrapper.
        /// </summary>
        /// <param name="transaction">Underlying EF Core transaction.</param>
        public EfUnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken ct = default)
        {
            return _transaction.CommitAsync(ct);
        }

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken ct = default)
        {
            return _transaction.RollbackAsync(ct);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}
