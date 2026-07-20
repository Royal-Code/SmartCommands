using RoyalCode.Repositories;
using RoyalCode.SmartProblems.Entities;
using System.Linq.Expressions;

namespace RoyalCode.SmartCommands.WorkContext.Adapters;

internal sealed class RepositoryAdapter<TEntity> : IRepositoryAccessor<TEntity>
    where TEntity : class
{
    private readonly IRepository<TEntity> repository;

    public RepositoryAdapter(IRepository<TEntity> repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<FindResult<TEntity, TId>> FindEntityAsync<TId>(Id<TEntity, TId> id, CancellationToken ct)
    {
        return repository.FindAsync(id, ct);
    }

    public Task<FindResult<TDto, TId>> FindEntityAsync<TDto, TId>(Id<TEntity, TId> id, CancellationToken ct) where TDto : class
    {
        return repository.FindAsync<TDto, TId>(id, ct);
    }

    public Task<FindResult<TDto>> FindEntityAsync<TDto>(
        Expression<Func<TEntity, bool>> filter,
        IReadOnlyList<FindCriterion> criteria,
        CancellationToken ct)
        where TDto : class
    {
        return repository.FindAsync<TDto>(filter, criteria, ct);
    }
}
