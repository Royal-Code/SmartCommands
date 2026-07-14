using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Opts the command into the full unit-of-work lifecycle backed by an <c>IWorkContext</c>
///     (from the <c>RoyalCode.SmartCommands.WorkContext</c> package). Equivalent to
///     <see cref="WithUnitOfWorkAttribute{T}"/> using the work context as the unit of work context.
/// </para>
/// <para>
///     Required by <see cref="WithRetryOnConcurrencyAttribute"/>, which retries the command body on
///     optimistic-concurrency conflicts. Cannot be combined with <see cref="WithUnitOfWorkAttribute{T}"/> or
///     <see cref="WithDbContextAttribute"/>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithWorkContextAttribute : Attribute { }