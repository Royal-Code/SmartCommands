using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Opts the command into the full unit-of-work lifecycle, using an <see cref="IUnitOfWorkAccessor{T}"/> of
///     context type <typeparamref name="T"/>: the generated handler begins the unit of work, loads any entities
///     the command needs, invokes the command method, and completes (persists) the unit of work.
/// </para>
/// <para>
///     Combine with <see cref="ProduceNewEntityAttribute"/> or <see cref="EditEntityAttribute{TEntity, TId}"/> to
///     create or edit an entity as part of the command.
/// </para>
/// </summary>
/// <typeparam name="T">The type of the context of the unit of work.</typeparam>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithUnitOfWorkAttribute<T> : Attribute
    where T : class
{ }
