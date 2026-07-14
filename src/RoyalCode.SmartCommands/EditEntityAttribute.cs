using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Marks the command method as editing an existing entity. The generated handler loads the entity of type
///     <typeparamref name="TEntity"/> by its identifier of type <typeparamref name="TId"/> before invoking the
///     command method, returning a NotFound problem when the entity does not exist.
/// </para>
/// <para>
///     Requires <see cref="WithUnitOfWorkAttribute{T}"/>. The loaded entity must be the first parameter of the command method.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The type of the entity being edited.</typeparam>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class EditEntityAttribute<TEntity, TId> : Attribute
    where TEntity : class
{ }