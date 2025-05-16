using Microsoft.EntityFrameworkCore;
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
    public async ValueTask BeginAsync(CancellationToken ct)
    {
        if (options.BeginTransations)
            await db.Database.BeginTransactionAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Result> CompleteAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);

            if (db.Database.CurrentTransaction is not null && options.BeginTransations)
            {
                await db.Database.CommitTransactionAsync(ct);
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {

            if (db.Database.CurrentTransaction is not null && options.BeginTransations)
            {
                try
                {
                    await db.Database.RollbackTransactionAsync(ct);
                }
                catch (Exception rollbackEx)
                {
                    return new AggregateException(
                        $"Multiple exceptions occurred while completing the transaction: {ex.Message}, {rollbackEx.Message}",
                        ex, rollbackEx);
                }
            }

            return ex;
        }
    }
}
