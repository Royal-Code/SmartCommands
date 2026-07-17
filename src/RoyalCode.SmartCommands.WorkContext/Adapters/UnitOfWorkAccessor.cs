using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.UnitOfWork;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.WorkContext.Adapters;

/// <summary>
/// <para>
///     A service that provides access to the unit of work.
/// </para>
/// </summary>
/// <typeparam name="TWorkContext">The type of the context of the unit of work.</typeparam>
public sealed class UnitOfWorkAccessor<TWorkContext> : IUnitOfWorkAccessor<TWorkContext>
    where TWorkContext : IWorkContext
{
    private readonly TWorkContext workContext;
    private readonly WorkContextAdapterOptions options;

    // transação iniciada por BeginAsync; o adapter só commita/reverte a transação que ele mesmo abriu,
    // nunca uma transação criada pelo usuário diretamente no contexto.
    private ITransaction? transaction;

    /// <summary>
    /// Creates a new instance of <see cref="UnitOfWorkAccessor{TWorkContext}"/>.
    /// </summary>
    /// <param name="workContext">The work context.</param>
    /// <param name="options">The options.</param>
    public UnitOfWorkAccessor(TWorkContext workContext, IOptions<WorkContextAdapterOptions> options)
    {
        this.workContext = workContext;
        this.options = options.Value;
    }

    /// <inheritdoc />
    public TWorkContext Context => workContext;

    /// <inheritdoc />
    public ValueTask AddEntityAsync<TEntity>(TEntity entity, CancellationToken ct)
        where TEntity : class
    {
        return workContext.AddAsync(entity, ct);
    }

    /// <inheritdoc />
    public async Task<FindResult<TEntity, TId>> FindEntityAsync<TEntity, TId>(TId id, CancellationToken ct)
        where TEntity : class
    {
        Id<TEntity, TId> entityId = id;
        return await workContext.Repository<TEntity>().FindAsync(entityId, ct);
    }

    /// <inheritdoc />
    public async Task<FindResult<TEntity, TId>> FindEntityAsync<TEntity, TId>(Id<TEntity, TId> id, CancellationToken ct) where TEntity : class
    {
        return await workContext.Repository<TEntity>().FindAsync(id, ct);
    }

    /// <inheritdoc />
    public async ValueTask BeginAsync(bool requireTransaction, CancellationToken ct)
    {
        // DF21: o comando pode exigir transação ([WithTransaction]) mesmo com a opção global desligada.
        // O adapter só assume ownership de transação que ainda não existia: a implementação real do
        // WorkContext adota (??=) uma transação já aberta e GetCurrentTransaction retorna o próprio
        // contexto, então iniciar aqui com transação pré-existente tomaria a transação do usuário.
        if ((options.BeginTransactions || requireTransaction) && workContext.GetCurrentTransaction() is null)
            transaction = await workContext.BeginTransactionAsync(ct);
    }

    /// <summary>
    /// <para>
    ///     Saves the work context and, when the adapter began a transaction, commits it.
    /// </para>
    /// <para>
    ///     Aligned with DF14: the returned <see cref="Result"/> represents only success or the problems
    ///     produced by the work context save; any exception — a concurrency conflict destined for the retry
    ///     loop, a cancellation or an unexpected failure — is caught only to attempt the transactional
    ///     cleanup and is then rethrown. Commit/rollback failures are no longer converted into the result.
    /// </para>
    /// </summary>
    public async Task<Result> CompleteAsync(CancellationToken ct)
    {
        Result result;
        try
        {
            result = await workContext.SaveAsync(ct);
        }
        catch (Exception ex)
        {
            // inclui ConcurrencyException (repassada ao laço de retry), cancelamento e falhas inesperadas
            await RollbackAsync(ex);
            throw;
        }

        if (!AdapterOwnsCurrentTransaction())
            return result;

        if (result.IsSuccess)
        {
            try
            {
                await transaction!.CommitAsync(ct);
                transaction = null;
            }
            catch (Exception ex)
            {
                await RollbackAsync(ex);
                throw;
            }
        }
        else
        {
            // problemas conhecidos do save: desfaz a transação do adapter e devolve o Result
            await RollbackAsync(cause: null);
        }

        return result;
    }

    /// <summary>
    /// Attempts to roll back the transaction begun by the adapter. Transactions not begun by the adapter
    /// are left to their owner.
    /// </summary>
    private async Task RollbackAsync(Exception? cause)
    {
        if (!AdapterOwnsCurrentTransaction())
            return;

        // o campo é limpo mesmo se o rollback falhar: o estado da transação é desconhecido e uma
        // nova tentativa de commit/rollback pelo adapter não seria segura
        var current = transaction!;
        transaction = null;

        try
        {
            // o token do handler pode já estar cancelado; a limpeza usa token próprio
            await current.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackEx)
        {
            if (cause is null)
                throw;

            throw new AggregateException(
                "The unit of work could not be completed and the transaction rollback also failed. " +
                "The first inner exception is the save/commit failure and the second is the rollback failure.",
                cause, rollbackEx);
        }
    }

    /// <summary>
    /// The adapter commits/rolls back only the transaction instance that <see cref="BeginAsync"/> created
    /// and that is still the work context's current transaction.
    /// </summary>
    private bool AdapterOwnsCurrentTransaction() =>
        transaction is not null && ReferenceEquals(workContext.GetCurrentTransaction(), transaction);
}
