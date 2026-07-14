using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Opts the command into the full unit-of-work lifecycle backed directly by an Entity Framework Core
///     <c>DbContext</c>, without requiring an explicit context type argument. Equivalent to
///     <see cref="WithUnitOfWorkAttribute{T}"/> using the ambient <c>DbContext</c> as the unit of work context.
/// </para>
/// <para>
///     Cannot be combined with <see cref="WithUnitOfWorkAttribute{T}"/> or <see cref="WithWorkContextAttribute"/>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithDbContextAttribute : Attribute { }
