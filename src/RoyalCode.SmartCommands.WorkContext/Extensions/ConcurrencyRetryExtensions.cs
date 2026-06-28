using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.UnitOfWork;

namespace RoyalCode.WorkContext;

/// <summary>
/// <para>
///     Extension methods that add optimistic-concurrency retry around a unit-of-work command body.
/// </para>
/// <para>
///     This is the runtime primitive used by the command handler generator for commands annotated with the
///     retry attribute, and it can also be called directly for token flows, where the retry must be scoped to
///     the aggregate mutation only (the single-use token consumption must stay outside the loop).
/// </para>
/// </summary>
public static class ConcurrencyRetryExtensions
{
    /// <summary>
    /// <para>
    ///     Generic detail used when the retry budget is exhausted and no custom problem is provided.
    /// </para>
    /// <para>
    ///     A fixed, generic message is used on purpose: the original <see cref="ConcurrencyException"/> message
    ///     carries provider/EF detail that should not leak to callers.
    /// </para>
    /// </summary>
    public const string ConcurrencyConflictDetail =
        "The operation could not be completed because the resource was modified by another process.";

    /// <summary>
    /// <para>
    ///     Executes <paramref name="body"/> and retries it when an optimistic-concurrency conflict
    ///     (<see cref="ConcurrencyException"/>) is raised while saving, up to <see cref="RetryOnConcurrencyOptions.MaxAttempts"/>.
    /// </para>
    /// <para>
    ///     Between attempts, any current transaction is rolled back (so partial work from the failed attempt is
    ///     undone) and the change tracker is cleared via <see cref="IUnitOfWork.CleanUp(bool)"/>, so the body
    ///     reloads fresh state from the store on the next attempt.
    /// </para>
    /// </summary>
    /// <param name="unitOfWork">The unit of work being retried; owns the change tracker and the transaction, if any.</param>
    /// <param name="body">
    ///     The operation to execute on each attempt; typically begins the unit of work, (re)loads the aggregate,
    ///     mutates it and saves. It must return a <see cref="Result"/> and must not include any non-idempotent,
    ///     immediately-committed side effect (e.g. single-use token consumption). The cancellation token is
    ///     captured by the caller, so the body has no token parameter.
    /// </param>
    /// <param name="options">
    ///     The retry policy. <see cref="RetryOnConcurrencyOptions.MaxAttempts"/> values lower than <c>1</c> are treated as <c>1</c>.
    /// </param>
    /// <param name="onExhausted">
    ///     Optional factory for the <see cref="Problem"/> returned when the retry budget is exhausted.
    ///     When <c>null</c>, a generic <see cref="Problems.InvalidState(string, string?, string?)"/> (409) problem is returned.
    /// </param>
    /// <param name="ct">The cancellation token used to roll back the transaction between attempts.</param>
    /// <returns>The result of <paramref name="body"/>, or a conflict problem when the attempts are exhausted.</returns>
    public static async Task<Result> RetryOnConcurrencyAsync(
        this IUnitOfWork unitOfWork,
        Func<Task<Result>> body,
        RetryOnConcurrencyOptions options,
        Func<Problem>? onExhausted = null,
        CancellationToken ct = default)
    {
        var maxAttempts = options.MaxAttempts < 1 ? 1 : options.MaxAttempts;
        var attempt = 0;

        while (true)
        {
            try
            {
                return await body();
            }
            catch (ConcurrencyException)
            {
                attempt++;

                // Revert any partial work from the failed attempt before reloading: the save threw before the
                // unit of work could roll back, so an open transaction would otherwise re-apply already-sent commands.
                var transaction = unitOfWork.GetCurrentTransaction();
                if (transaction is not null)
                    await transaction.RollbackAsync(ct);

                // Detach tracked entities so the next attempt reloads fresh state from the store.
                unitOfWork.CleanUp();

                if (attempt >= maxAttempts)
                    return onExhausted?.Invoke() ?? Problems.InvalidState(ConcurrencyConflictDetail);
            }
        }
    }
}
