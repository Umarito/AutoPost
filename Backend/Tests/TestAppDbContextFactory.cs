using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Factory for creating isolated in-memory database contexts for unit tests.
///
/// <para><b>Why:</b>
/// Each test method must run against a clean database to prevent data leakage between tests.
/// Using a unique database name per test (Guid suffix) guarantees complete isolation — even when
/// tests run in parallel. The InMemory provider is chosen for speed (no disk I/O) and for
/// testing pure EF Core query logic without external dependencies.</para>
///
/// <para><b>How:</b>
/// Generates a unique database name by combining a caller-provided base name with a GUID,
/// then creates an <see cref="ApplicationDbContext"/> configured with the InMemory provider.
/// The context is fully functional for CRUD operations but does NOT enforce relational
/// constraints (foreign keys, unique indexes) — those are validated by integration tests.</para>
/// </summary>
internal static class TestAppDbContextFactory
{
    /// <summary>
    /// Creates a new <see cref="ApplicationDbContext"/> backed by a unique in-memory database.
    /// Each call produces a completely isolated database instance.
    /// </summary>
    /// <param name="databaseName">
    /// A descriptive base name for the database (e.g., "WorkspaceRepo_GetById").
    /// A GUID is appended automatically to ensure uniqueness across parallel test runs.
    /// </param>
    /// <returns>A configured <see cref="ApplicationDbContext"/> ready for use in tests.</returns>
    public static ApplicationDbContext CreateContext(string databaseName)
    {
        var uniqueName = $"{databaseName}_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(uniqueName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
