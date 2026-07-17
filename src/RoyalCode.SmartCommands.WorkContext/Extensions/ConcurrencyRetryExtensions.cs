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
    ///     Change-tracker cleanup is still attempted when rollback fails; cleanup failures preserve the original
    ///     conflict and are reported together in an <see cref="AggregateException"/>.
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
    /// <param name="ct">
    ///     Used only to stop retrying (after the cleanup) when cancellation is requested; the between-attempts
    ///     rollback runs on its own token so the cleanup is never aborted before it runs.
    /// </param>
    /// <returns>The result of <paramref name="body"/>, or a conflict problem when the attempts are exhausted.</returns>
    public static Task<Result> RetryOnConcurrencyAsync(
        this IUnitOfWork unitOfWork,
        Func<Task<Result>> body,
        RetryOnConcurrencyOptions options,
        Func<Problem>? onExhausted = null,
        CancellationToken ct = default)
        => RetryOnConcurrencyCoreAsync(
            unitOfWork,
            body,
            options,
            onExhausted,
            static problem => problem,
            ct);

    /// <summary>
    /// <para>
    ///     Value-carrying variant of <see cref="RetryOnConcurrencyAsync(IUnitOfWork, Func{Task{Result}}, RetryOnConcurrencyOptions, Func{Problem}, CancellationToken)"/>:
    ///     executes <paramref name="body"/> and retries it when an optimistic-concurrency conflict
    ///     (<see cref="ConcurrencyException"/>) is raised while saving, up to <see cref="RetryOnConcurrencyOptions.MaxAttempts"/>,
    ///     returning the typed value produced by the body (e.g. the entity created by <c>ProduceNewEntity</c>).
    /// </para>
    /// <para>
    ///     Between attempts, any current transaction is rolled back and the change tracker is cleared via
    ///     <see cref="IUnitOfWork.CleanUp(bool)"/>, so the body reloads fresh state on the next attempt. The same
    ///     re-execution contract of the non-generic overload applies: the body must be safe to run again (no
    ///     non-idempotent, immediately-committed side effect).
    ///     Change-tracker cleanup is still attempted when rollback fails, preserving every cleanup failure.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The value type carried by the <see cref="Result{T}"/> returned by the body.</typeparam>
    /// <param name="unitOfWork">The unit of work being retried; owns the change tracker and the transaction, if any.</param>
    /// <param name="body">The operation to execute on each attempt; must return a <see cref="Result{T}"/>.</param>
    /// <param name="options">The retry policy. <see cref="RetryOnConcurrencyOptions.MaxAttempts"/> values lower than <c>1</c> are treated as <c>1</c>.</param>
    /// <param name="onExhausted">
    ///     Optional factory for the <see cref="Problem"/> returned when the retry budget is exhausted.
    ///     When <c>null</c>, a generic <see cref="Problems.InvalidState(string, string?, string?)"/> (409) problem is returned.
    /// </param>
    /// <param name="ct">
    ///     Used only to stop retrying (after the cleanup) when cancellation is requested; the between-attempts
    ///     rollback runs on its own token so the cleanup is never aborted before it runs.
    /// </param>
    /// <returns>The result of <paramref name="body"/>, or a conflict problem when the attempts are exhausted.</returns>
    public static Task<Result<T>> RetryOnConcurrencyAsync<T>(
        this IUnitOfWork unitOfWork,
        Func<Task<Result<T>>> body,
        RetryOnConcurrencyOptions options,
        Func<Problem>? onExhausted = null,
        CancellationToken ct = default)
        => RetryOnConcurrencyCoreAsync(
            unitOfWork,
            body,
            options,
            onExhausted,
            static problem => problem,
            ct);

    private static async Task<TResult> RetryOnConcurrencyCoreAsync<TResult>(
        IUnitOfWork unitOfWork,
        Func<Task<TResult>> body,
        RetryOnConcurrencyOptions options,
        Func<Problem>? onExhausted,
        Func<Problem, TResult> problemResultFactory,
        CancellationToken ct)
    {
        var maxAttempts = options.MaxAttempts < 1 ? 1 : options.MaxAttempts;
        var attempt = 0;

        while (true)
        {
            try
            {
                return await body();
            }
            catch (ConcurrencyException conflict)
            {
                attempt++;
                await CleanUpFailedAttemptAsync(unitOfWork, conflict);

                if (attempt >= maxAttempts)
                {
                    var problem = onExhausted?.Invoke() ?? Problems.InvalidState(ConcurrencyConflictDetail);
                    return problemResultFactory(problem);
                }

                // cancellation stays cancellation: after the cleanup, do not start a new attempt
                ct.ThrowIfCancellationRequested();
            }
        }
    }

    private static async ValueTask CleanUpFailedAttemptAsync(
        IUnitOfWork unitOfWork,
        ConcurrencyException conflict)
    {
        Exception? rollbackFailure = null;
        Exception? trackerCleanupFailure = null;

        var transaction = unitOfWork.GetCurrentTransaction();
        if (transaction is not null)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                rollbackFailure = ex;
            }
        }

        // Always detach tracked entities, even when rollback fails. No new attempt will start after a cleanup
        // failure, but callers that inspect or dispose the context do not retain the failed attempt's entities.
        try
        {
            unitOfWork.CleanUp();
        }
        catch (Exception ex)
        {
            trackerCleanupFailure = ex;
        }

        if (rollbackFailure is null && trackerCleanupFailure is null)
            return;

        var failures = new List<Exception> { conflict };
        if (rollbackFailure is not null)
            failures.Add(rollbackFailure);
        if (trackerCleanupFailure is not null)
            failures.Add(trackerCleanupFailure);

        throw new AggregateException(
            "The optimistic-concurrency retry could not clean up the failed attempt. " +
            "The first inner exception is the concurrency conflict; subsequent exceptions are rollback/change-tracker cleanup failures.",
            failures);
    }
}
