using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Indicates that the command method creates a new entity, which is added to the unit of work and
///     persisted when the unit of work completes. The value returned by the command method is the new entity.
/// </para>
/// <para>
///     Requires <see cref="WithUnitOfWorkAttribute{T}"/>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class ProduceNewEntityAttribute : Attribute { }