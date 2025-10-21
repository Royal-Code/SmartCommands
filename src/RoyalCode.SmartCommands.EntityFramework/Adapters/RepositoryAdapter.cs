using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartProblems.Entities;

namespace RoyalCode.SmartCommands.EntityFramework.Adapters;

/// <summary>
/// Provides a base implementation of <see cref="IRepositoryAccessor{TEntity}"/> backed by an EF Core <see cref="DbContext"/>.
/// Enables finding entities by id and projecting them to DTOs.
/// </summary>
/// <typeparam name="TEntity">Entity type managed by this repository accessor.</typeparam>
/// <typeparam name="TContext">Concrete <see cref="DbContext"/> type used for data access.</typeparam>
public abstract class RepositoryAdapter<TEntity, TContext> : IRepositoryAccessor<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    /// <summary>
    /// The underlying Entity Framework Core context used to access the database.
    /// </summary>
    private readonly DbContext db;

    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryAdapter{TEntity, TContext}"/> class.
    /// </summary>
    /// <param name="db">The EF Core context instance; must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="db"/> is null.</exception>
    protected RepositoryAdapter(TContext db)
    {
        this.db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// Finds an entity by its identifier.
    /// </summary>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <param name="id">The strongly typed identifier value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="FindResult{TEntity, TId}"/> containing the entity when found or a result with a NotFound problem.
    /// </returns>
    public async Task<FindResult<TEntity, TId>> FindEntityAsync<TId>(Id<TEntity, TId> id, CancellationToken ct)
    {
        return await db.Set<TEntity>().TryFindAsync(id, ct);
    }

    /// <summary>
    /// Finds an entity by its identifier and projects it to a DTO type.
    /// </summary>
    /// <typeparam name="TDto">The DTO type to project to.</typeparam>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <param name="id">The strongly typed identifier value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="FindResult{TDto, TId}"/> containing the projected DTO when found or a result with a NotFound problem.
    /// </returns>
    public abstract Task<FindResult<TDto, TId>> FindEntityAsync<TDto, TId>(Id<TEntity, TId> id, CancellationToken ct)
        where TDto : class;
}
