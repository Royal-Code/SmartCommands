using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.WorkContext.Abstractions;

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
    public async ValueTask BeginAsync(CancellationToken ct)
    {
        if (options.BeginTransactions)
        {
            await workContext.BeginTransactionAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<Result> CompleteAsync(CancellationToken ct)
    {
        Result result = await workContext.SaveAsync(ct);

        if (!options.BeginTransactions)
            return result;

        var transaction = workContext.GetCurrentTransaction();
        if (transaction is null)
            return result;

        try
        {
            if (result.IsSuccess)
                await transaction.CommitAsync(ct);
            else
                await transaction.RollbackAsync(ct);
        }
        catch (Exception ex)
        {
            result += ex;
        }

        return result;
    }
}
