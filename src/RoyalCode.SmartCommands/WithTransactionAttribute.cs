using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Requires a transaction for the command's unit of work, regardless of the adapter option
///     (<c>BeginTransactions</c>). The generated handler calls
///     <c>IUnitOfWorkAccessor&lt;T&gt;.BeginAsync(requireTransaction: true, ct)</c>.
/// </para>
/// <para>
///     Use it when the command body produces more than one committed write before the final complete
///     (e.g. intermediate saves), so a failure — or an optimistic-concurrency retry — can roll back the
///     partial work of the attempt. Commands with a single final save usually do not need it: the unit
///     of work save is already atomic.
/// </para>
/// <para>
///     Requires a unit of work (<see cref="WithUnitOfWorkAttribute{TContext}"/>, <c>WithDbContext</c> or
///     <see cref="WithWorkContextAttribute"/>); without one, the generator reports RCCMD043. This attribute
///     is opt-in only: there is no per-command way to disable transactions enabled by the adapter option.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public sealed class WithTransactionAttribute : Attribute;
