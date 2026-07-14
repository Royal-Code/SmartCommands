using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Opts the command into loading entities before executing the command method, using an
///     <see cref="IRepositoriesAccessor{T}"/> of context type <typeparamref name="T"/>, without starting a
///     full unit of work (no begin/complete transaction lifecycle).
/// </para>
/// <para>
///     Entity parameters (or collections of entities) with a matching id property or parameter in the command
///     class are resolved and loaded automatically, returning a NotFound problem when an entity is missing.
///     Use <see cref="WithUnitOfWorkAttribute{T}"/> instead when the command also needs to persist changes.
/// </para>
/// </summary>
/// <typeparam name="T">The type of the context used to access the repositories.</typeparam>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithFindEntitiesAttribute<T> : Attribute
    where T : class
{ }