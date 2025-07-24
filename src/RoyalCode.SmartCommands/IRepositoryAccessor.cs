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
    ///     An entry that represents the entity find by the identifier.
    ///     Even if the entity is not found, the method must return an Entry object with the NotFound problem.
    /// </returns>
    public Task<FindResult<TEntity, TId>> FindEntityAsync<TId>(Id<TEntity, TId> id, CancellationToken ct);
}
