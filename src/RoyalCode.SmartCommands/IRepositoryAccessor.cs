using RoyalCode.SmartProblems.Entities;


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
}
