using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands.EntityFramework.Options;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;

namespace RoyalCode.SmartCommands.EntityFramework.Adapters;

/// <summary>
/// <para>
///     A service that provides access to the unit of work.
/// </para>
/// </summary>
/// <typeparam name="TContext">The type of the context of the unit of work.</typeparam>
public sealed class DbContextAccessor<TContext> : IUnitOfWorkAccessor<TContext>
    where TContext : DbContext
{
    private readonly TContext db;
    private readonly DbContextAdapterOptions options;

    // transação iniciada por BeginAsync; o adapter só commita/reverte a transação que ele mesmo abriu,
    // nunca uma transação criada pelo usuário diretamente no contexto.
    private IDbContextTransaction? transaction;

    /// <summary>
    /// Creates a new instance of <see cref="DbContextAccessor{TContext}"/>.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="options">The options.</param>
    public DbContextAccessor(TContext db, IOptions<DbContextAdapterOptions> options)
    {
        this.db = db;
        this.options = options.Value;
    }

    /// <inheritdoc />
    public TContext Context => db;

    /// <inheritdoc />
    public async ValueTask AddEntityAsync<TEntity>(TEntity entity, CancellationToken ct) where TEntity : class
    {
        await db.AddAsync(entity, ct);
    }

    /// <inheritdoc />
    public async Task<FindResult<TEntity, TId>> FindEntityAsync<TEntity, TId>(TId id, CancellationToken ct)
        where TEntity : class
    {
        var entity = await db.FindAsync<TEntity>([id], ct);

        return new FindResult<TEntity, TId>(entity, id);
    }

    /// <inheritdoc />
    public Task<FindResult<TEntity, TId>> FindEntityAsync<TEntity, TId>(Id<TEntity, TId> id, CancellationToken ct)
        where TEntity : class
    {
        return db.TryFindAsync(id, ct);
    }

    /// <inheritdoc />
    public async ValueTask BeginAsync(bool requireTransaction, CancellationToken ct)
    {
        // DF21: o comando pode exigir transação ([WithTransaction]) mesmo com a opção global desligada
        if (options.BeginTransactions || requireTransaction)
            transaction = await db.Database.BeginTransactionAsync(ct);
    }

    /// <summary>
    /// <para>
    ///     Generic detail used when the save fails with an optimistic-concurrency conflict.
    /// </para>
    /// <para>
    ///     A fixed, generic message is used on purpose: the original <see cref="DbUpdateConcurrencyException"/>
    ///     message carries provider/EF detail that should not leak to callers.
    /// </para>
    /// </summary>
    public const string ConcurrencyConflictDetail =
        "The operation could not be completed because the resource was modified by another process.";

    /// <summary>
    /// <para>
    ///     Saves the changes and, when the adapter began a transaction, commits it.
    /// </para>
    /// <para>
    ///     The returned <see cref="Result"/> represents only success or explicitly known problems:
    ///     an optimistic-concurrency conflict (<see cref="DbUpdateConcurrencyException"/>) becomes an
    ///     invalid-state problem with a generic detail. Any other exception — including
    ///     <see cref="OperationCanceledException"/> — is caught only to attempt the transactional
    ///     cleanup and is then rethrown, so cancellation stays cancellation and unexpected failures
    ///     reach the caller's exception handling (e.g. the app's HTTP exception middleware).
    /// </para>
    /// </summary>
    public async Task<Result> CompleteAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);

            if (AdapterOwnsCurrentTransaction())
            {
                var ownedTransaction = transaction!;
                await ownedTransaction.CommitAsync(ct);
                transaction = null;
                await ownedTransaction.DisposeAsync();
            }

            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await RollbackAsync(ex);
            return Problems.InvalidState(ConcurrencyConflictDetail);
        }
        catch (Exception ex)
        {
            await RollbackAsync(ex);
            throw;
        }
    }

    /// <summary>
    /// Attempts to roll back the transaction begun by the adapter after <paramref name="cause"/> interrupted
    /// the save/commit. Transactions not begun by the adapter are left to their owner.
    /// </summary>
    private async Task RollbackAsync(Exception cause)
    {
        if (!AdapterOwnsCurrentTransaction())
            return;

        var ownedTransaction = transaction!;
        Exception? cleanupFailure = null;

        try
        {
            // the handler token may already be cancelled at this point; the cleanup uses its own token
            // so the rollback attempt is not aborted before it runs.
            await ownedTransaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackEx)
        {
            cleanupFailure = rollbackEx;
        }

        transaction = null;

        try
        {
            await ownedTransaction.DisposeAsync();
        }
        catch (Exception disposeEx)
        {
            cleanupFailure = cleanupFailure is null
                ? disposeEx
                : new AggregateException(
                    "The transaction rollback and disposal both failed.",
                    cleanupFailure,
                    disposeEx);
        }

        if (cleanupFailure is not null)
        {
            throw new AggregateException(
                "The unit of work could not be completed and the transaction cleanup also failed. " +
                "The first inner exception is the save/commit failure and the second is the rollback/dispose failure.",
                cause, cleanupFailure);
        }
    }

    /// <summary>
    /// The adapter commits/rolls back only the transaction instance that <see cref="BeginAsync"/> created
    /// and that is still the context's current transaction.
    /// </summary>
    private bool AdapterOwnsCurrentTransaction() =>
        transaction is not null && ReferenceEquals(db.Database.CurrentTransaction, transaction);
}
