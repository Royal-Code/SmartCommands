using RoyalCode.SmartProblems.Entities;
using System.Linq.Expressions;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     A service that provides access to the repository for a specific entity type.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public interface IRepositoryAccessor<TEntity>
    where TEntity : class
{
    /// <summary>
    /// <para>
    ///     Finds an entity by its identifier.
    /// </para>
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="ct">Cancellation token.</param>
    /// 
    /// <typeparam name="TId">The type of the identifier.</typeparam>
    /// <returns>
    ///     A result that represents the entity find by the identifier.
    ///     Even if the entity is not found, the method must return a result object with the NotFound problem.
    /// </returns>
    public Task<FindResult<TEntity, TId>> FindEntityAsync<TId>(Id<TEntity, TId> id, CancellationToken ct);

    /// <summary>
    /// <para>
    ///     Finds an entity by its identifier and select a DTO (Data Transfer Object) representation of it.
    /// </para>
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <typeparam name="TDto">The type of the Data Transfer Object (DTO) to select.</typeparam>
    /// <typeparam name="TId">The type of the identifier.</typeparam>
    /// <returns>
    ///     A result that represents the entity find by the identifier.
    ///     Even if the entity is not found, the method must return a result object with the NotFound problem.
    /// </returns>
    public Task<FindResult<TDto, TId>> FindEntityAsync<TDto, TId>(Id<TEntity, TId> id, CancellationToken ct)
        where TDto : class;

    /// <summary>
    /// <para>
    ///     Finds an entity by a filter expression (alternate/composite key) and selects a DTO
    ///     (Data Transfer Object) representation of it. The projection must be executed by the
    ///     provider, without materializing or tracking the entity.
    /// </para>
    /// <para>
    ///     When the entity is not found, the <paramref name="criteria"/> generate a rich not-found
    ///     problem naming the entity, without analyzing the filter expression at runtime — the
    ///     generated code already knows the properties and values used by the filter.
    /// </para>
    /// </summary>
    /// <param name="filter">The filter expression to apply.</param>
    /// <param name="criteria">The criteria used by the filter, in declaration order, for the not-found problem.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <typeparam name="TDto">The type of the Data Transfer Object (DTO) to select.</typeparam>
    /// <returns>
    ///     A result that represents the DTO selected from the entity found by the filter.
    ///     Even if the entity is not found, the method must return a result object with the NotFound problem.
    /// </returns>
    public Task<FindResult<TDto>> FindEntityAsync<TDto>(
        Expression<Func<TEntity, bool>> filter,
        IReadOnlyList<FindCriterion> criteria,
        CancellationToken ct)
        where TDto : class;
}
