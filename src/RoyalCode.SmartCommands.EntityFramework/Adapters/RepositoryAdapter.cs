using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartProblems.Entities;
using System.Linq.Expressions;

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
    /// <para>
    ///     The concrete Entity Framework Core context used to access the database.
    /// </para>
    /// <para>
    ///     Exposed to subclasses so projections (<see cref="FindEntityAsync{TDto, TId}(Id{TEntity, TId}, CancellationToken)"/>)
    ///     can be implemented against the typed context without storing a second reference to it.
    /// </para>
    /// </summary>
    protected TContext Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryAdapter{TEntity, TContext}"/> class.
    /// </summary>
    /// <param name="db">The EF Core context instance; must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="db"/> is null.</exception>
    protected RepositoryAdapter(TContext db)
    {
        Context = db ?? throw new ArgumentNullException(nameof(db));
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
        return await Context.Set<TEntity>().TryFindAsync(id, ct);
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

    /// <summary>
    /// <para>
    ///     Finds an entity by a filter expression (alternate/composite key) and projects it to a DTO type.
    /// </para>
    /// <para>
    ///     Implement it with the protected
    ///     <see cref="FindEntityAsync{TDto}(Expression{Func{TEntity, bool}}, IReadOnlyList{FindCriterion}, Expression{Func{TEntity, TDto}}, CancellationToken)"/>
    ///     helper, supplying the projection expression: the query runs in the provider, without
    ///     materializing or tracking the entity, and the not-found problem names the entity.
    /// </para>
    /// </summary>
    /// <typeparam name="TDto">The DTO type to project to.</typeparam>
    /// <param name="filter">The filter expression to apply.</param>
    /// <param name="criteria">The criteria used by the filter, in declaration order, for the not-found problem.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="FindResult{TDto}"/> containing the projected DTO when found or a result with a NotFound problem.
    /// </returns>
    public abstract Task<FindResult<TDto>> FindEntityAsync<TDto>(
        Expression<Func<TEntity, bool>> filter,
        IReadOnlyList<FindCriterion> criteria,
        CancellationToken ct)
        where TDto : class;

    /// <summary>
    /// <para>
    ///     Executes the filtered projection in the provider (<c>Where(filter).Select(selector).FirstOrDefaultAsync</c>):
    ///     a single query, selecting only the DTO columns, without tracking the entity.
    /// </para>
    /// <para>
    ///     When no row matches, the result carries a rich not-found problem generated from the
    ///     <paramref name="criteria"/>, naming the entity (<typeparamref name="TEntity"/>) instead of the DTO.
    /// </para>
    /// </summary>
    /// <typeparam name="TDto">The DTO type to project to.</typeparam>
    /// <param name="filter">The filter expression to apply.</param>
    /// <param name="criteria">The criteria used by the filter, in declaration order, for the not-found problem.</param>
    /// <param name="selector">The projection expression from the entity to the DTO, translatable by the provider.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="FindResult{TDto}"/> containing the projected DTO when found or a result with a NotFound problem.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     If <paramref name="filter"/>, <paramref name="criteria"/> or <paramref name="selector"/> is null.
    /// </exception>
    protected async Task<FindResult<TDto>> FindEntityAsync<TDto>(
        Expression<Func<TEntity, bool>> filter,
        IReadOnlyList<FindCriterion> criteria,
        Expression<Func<TEntity, TDto>> selector,
        CancellationToken ct)
        where TDto : class
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(selector);

        var dto = await Context.Set<TEntity>()
            .Where(filter)
            .Select(selector)
            .FirstOrDefaultAsync(ct);

        return FindResult<TDto>.ProjectedFrom<TEntity>(dto, criteria);
    }
}
