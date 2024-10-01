using RoyalCode.SmartValidations.Entities;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     A service that provides access to the repositories and the context of the database (unit of work).
/// </para>
/// </summary>
/// <typeparam name="T">The type of the context of the unit of work.</typeparam>
public interface IRepositoriesAccessor<out T>
{
    /// <summary>
    /// <para>
    ///     Gets the context of the unit of work.
    /// </para>
    /// </summary>
    public T Context { get; }

    /// <summary>
    /// <para>
    ///     Finds an entity by its identifier.
    /// </para>
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the identifier.</typeparam>
    /// <returns>
    ///     An entry that represents the entity find by the identifier.
    ///     Even if the entity is not found, the method must return an Entry object with the NotFound problem.
    /// </returns>
    public Task<Entry<TEntity, TId>> FindEntityAsync<TEntity, TId>(TId id, CancellationToken ct)
        where TEntity : class;

    /// <summary>
    /// <para>
    ///     Adds a new entity to the repository.
    /// </para>
    /// <para>
    ///     This operation must not apply changes to the database immediately. It must take place within a work unit.
    /// </para>
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entity">The new entity.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///     A value task for possible asynchronous scenarios.
    /// </returns>
    public ValueTask AddEntityAsync<TEntity>(TEntity entity, CancellationToken ct)
        where TEntity : class;
}