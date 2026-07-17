// Ignore Spelling: Accessor

using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     A service that provides access to the unit of work.
/// </para>
/// </summary>
/// <typeparam name="T">The type of the context of the unit of work.</typeparam>
public interface IUnitOfWorkAccessor<out T> : IRepositoriesAccessor<T>
{
    /// <summary>
    /// <para>
    ///     Invoked when the unit of work is about to begin.
    /// </para>
    /// <para>
    ///     A transaction is started when the adapter option enables transactions or when
    ///     <paramref name="requireTransaction"/> is <c>true</c> (DF21: the generated handler passes
    ///     <c>true</c> for commands annotated with <c>[WithTransaction]</c>). The adapter owns only the
    ///     transaction it started here; transactions opened directly by the user belong to the user.
    /// </para>
    /// </summary>
    /// <param name="requireTransaction">When <c>true</c>, a transaction is always started, regardless of the adapter option.</param>
    /// <param name="ct">Cancellation token.</param>
    public ValueTask BeginAsync(bool requireTransaction, CancellationToken ct);

    /// <summary>
    /// <para>
    ///     Invoked when the unit of work is about to complete.
    /// </para>
    /// <para>
    ///     At this point, the unit of work should be committed (or the save changes should be called).
    /// </para>
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public Task<Result> CompleteAsync(CancellationToken ct);
}