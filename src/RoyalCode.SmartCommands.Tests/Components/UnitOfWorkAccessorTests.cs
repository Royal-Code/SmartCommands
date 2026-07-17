using Microsoft.Extensions.Options;
using RoyalCode.OperationHint.Abstractions;
using RoyalCode.Repositories;
using RoyalCode.SmartCommands.WorkContext.Adapters;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.SmartSearch;
using RoyalCode.UnitOfWork;
using RoyalCode.WorkContext;
using RoyalCode.WorkContext.Commands;
using RoyalCode.WorkContext.Querying;

namespace RoyalCode.SmartCommands.Tests.Components;

/// <summary>
/// Fase 8: <see cref="UnitOfWorkAccessor{TWorkContext}"/> alinhado ao DF14 — o <c>Result</c> do
/// <c>CompleteAsync</c> representa somente sucesso ou os problemas do save; exceções (concorrência
/// para o retry, cancelamento, falhas inesperadas) atravessam após a limpeza transacional; o adapter
/// só commita/reverte a transação que o próprio <c>BeginAsync</c> criou (DF21).
/// </summary>
public class UnitOfWorkAccessorTests
{
    private static UnitOfWorkAccessor<FakeWorkContext> CreateAccessor(FakeWorkContext context, bool beginTransactions)
        => new(context, Options.Create(new WorkContextAdapterOptions { BeginTransactions = beginTransactions }));

    [Fact]
    public async Task Sucesso_sem_transacao_retorna_result_do_save()
    {
        var context = new FakeWorkContext();
        var accessor = CreateAccessor(context, beginTransactions: false);

        await accessor.BeginAsync(requireTransaction: false, CancellationToken.None);
        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, context.BeginCount);
    }

    [Fact]
    public async Task Opcao_ligada_cria_transacao_e_commita_uma_vez()
    {
        var context = new FakeWorkContext();
        var accessor = CreateAccessor(context, beginTransactions: true);

        await accessor.BeginAsync(requireTransaction: false, CancellationToken.None);
        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, context.BeginCount);
        Assert.Equal(1, context.LastTransaction!.CommitCount);
        Assert.Equal(0, context.LastTransaction.RollbackCount);
    }

    [Fact]
    public async Task WithTransaction_exige_transacao_mesmo_com_a_opcao_desligada()
    {
        var context = new FakeWorkContext();
        var accessor = CreateAccessor(context, beginTransactions: false);

        // DF21: o handler gerado passa requireTransaction: true para comandos [WithTransaction]
        await accessor.BeginAsync(requireTransaction: true, CancellationToken.None);
        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, context.BeginCount);
        Assert.Equal(1, context.LastTransaction!.CommitCount);
    }

    [Fact]
    public async Task Problemas_do_save_fazem_rollback_e_retornam_o_result()
    {
        var context = new FakeWorkContext
        {
            OnSave = () => Task.FromResult(new SaveResult(new InvalidOperationException("known failure"))),
        };
        var accessor = CreateAccessor(context, beginTransactions: true);

        await accessor.BeginAsync(requireTransaction: false, CancellationToken.None);
        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.HasProblems(out _));
        Assert.Equal(0, context.LastTransaction!.CommitCount);
        Assert.Equal(1, context.LastTransaction.RollbackCount);
        // a limpeza usa token próprio, nunca o token do handler
        Assert.Equal(CancellationToken.None, context.LastTransaction.LastRollbackToken);
    }

    [Fact]
    public async Task Excecao_do_save_atravessa_apos_rollback_da_transacao_do_adapter()
    {
        var saveFailure = new ConcurrencyException("conflict", new InvalidOperationException());
        var context = new FakeWorkContext { OnSave = () => throw saveFailure };
        var accessor = CreateAccessor(context, beginTransactions: true);

        await accessor.BeginAsync(requireTransaction: false, CancellationToken.None);
        var thrown = await Assert.ThrowsAsync<ConcurrencyException>(
            () => accessor.CompleteAsync(CancellationToken.None));

        // a exceção do laço de retry atravessa intacta; o adapter só faz a limpeza
        Assert.Same(saveFailure, thrown);
        Assert.Equal(1, context.LastTransaction!.RollbackCount);
        Assert.Equal(0, context.LastTransaction.CommitCount);
    }

    [Fact]
    public async Task Cancelamento_permanece_cancelamento_e_o_rollback_usa_token_proprio()
    {
        using var cts = new CancellationTokenSource();
        var context = new FakeWorkContext
        {
            OnSave = () =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
                return Task.FromResult(new SaveResult(1));
            },
        };
        var accessor = CreateAccessor(context, beginTransactions: true);

        await accessor.BeginAsync(requireTransaction: false, CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => accessor.CompleteAsync(cts.Token));

        Assert.Equal(1, context.LastTransaction!.RollbackCount);
        Assert.Equal(CancellationToken.None, context.LastTransaction.LastRollbackToken);
    }

    [Fact]
    public async Task Falha_do_commit_atravessa_apos_rollback()
    {
        var commitFailure = new InvalidOperationException("commit boom");
        var context = new FakeWorkContext();
        var accessor = CreateAccessor(context, beginTransactions: true);

        await accessor.BeginAsync(requireTransaction: false, CancellationToken.None);
        context.LastTransaction!.ThrowOnCommit = commitFailure;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => accessor.CompleteAsync(CancellationToken.None));

        // antes da Fase 8 a falha era convertida em Result (result += ex); agora atravessa
        Assert.Same(commitFailure, thrown);
        Assert.Equal(1, context.LastTransaction.RollbackCount);
    }

    [Fact]
    public async Task Falha_do_commit_e_do_rollback_preserva_as_duas_excecoes()
    {
        var commitFailure = new InvalidOperationException("commit boom");
        var rollbackFailure = new IOException("rollback boom");
        var context = new FakeWorkContext();
        var accessor = CreateAccessor(context, beginTransactions: true);

        await accessor.BeginAsync(requireTransaction: false, CancellationToken.None);
        context.LastTransaction!.ThrowOnCommit = commitFailure;
        context.LastTransaction.ThrowOnRollback = rollbackFailure;

        var aggregate = await Assert.ThrowsAsync<AggregateException>(
            () => accessor.CompleteAsync(CancellationToken.None));

        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Same(commitFailure, aggregate.InnerExceptions[0]);
        Assert.Same(rollbackFailure, aggregate.InnerExceptions[1]);
    }

    [Fact]
    public async Task Transacao_do_usuario_nao_e_commitada_nem_revertida_pelo_adapter()
    {
        var context = new FakeWorkContext();
        var accessor = CreateAccessor(context, beginTransactions: true);

        // o usuário abriu a própria transação no contexto, sem BeginAsync do adapter
        var userTransaction = new FakeTransaction();
        context.CurrentTransaction = userTransaction;

        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, userTransaction.CommitCount);
        Assert.Equal(0, userTransaction.RollbackCount);
    }

    [Fact]
    public async Task BeginAsync_nao_inicia_nem_assume_transacao_pre_existente_do_usuario()
    {
        // a implementação real do WorkContext adota (??=) transações já abertas; o adapter evita
        // isso não iniciando quando GetCurrentTransaction já retorna algo — a transação segue do usuário
        var context = new FakeWorkContext();
        var userTransaction = new FakeTransaction();
        context.CurrentTransaction = userTransaction;
        var accessor = CreateAccessor(context, beginTransactions: true);

        await accessor.BeginAsync(requireTransaction: true, CancellationToken.None);

        Assert.Equal(0, context.BeginCount);

        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, userTransaction.CommitCount);
        Assert.Equal(0, userTransaction.RollbackCount);
    }

    private sealed class FakeTransaction : ITransaction
    {
        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public CancellationToken? LastRollbackToken { get; private set; }

        public Exception? ThrowOnCommit { get; set; }

        public Exception? ThrowOnRollback { get; set; }

        public void Commit() => throw new NotSupportedException();

        public void Rollback() => throw new NotSupportedException();

        public Task CommitAsync(CancellationToken ct)
        {
            if (ThrowOnCommit is not null)
                throw ThrowOnCommit;

            CommitCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken ct)
        {
            if (ThrowOnRollback is not null)
                throw ThrowOnRollback;

            RollbackCount++;
            LastRollbackToken = ct;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWorkContext : IWorkContext
    {
        public int BeginCount { get; private set; }

        public FakeTransaction? LastTransaction { get; private set; }

        public ITransaction? CurrentTransaction { get; set; }

        public Func<Task<SaveResult>>? OnSave { get; set; }

        // --- IUnitOfWork ---

        public SaveResult Save() => throw new NotSupportedException();

        public Task<SaveResult> SaveAsync(CancellationToken token = default)
            => OnSave?.Invoke() ?? Task.FromResult(new SaveResult(1));

        public ITransaction? GetCurrentTransaction() => CurrentTransaction;

        public ITransaction BeginTransaction() => throw new NotSupportedException();

        public Task<ITransaction> BeginTransactionAsync(CancellationToken token = default)
        {
            BeginCount++;
            LastTransaction = new FakeTransaction();
            CurrentTransaction = LastTransaction;
            return Task.FromResult<ITransaction>(LastTransaction);
        }

        public void CleanUp(bool force = true) { }

        // --- membros não usados pelo accessor ---

        public IRepository<TEntity> Repository<TEntity>() where TEntity : class => throw new NotSupportedException();

        public ICriteria<TEntity> Criteria<TEntity>() where TEntity : class => throw new NotSupportedException();

        public Task<IEnumerable<TEntity>> QueryAsync<TEntity>(IQueryRequest<TEntity> request, CancellationToken ct = default)
            where TEntity : class => throw new NotSupportedException();

        public Task<IEnumerable<TModel>> QueryAsync<TEntity, TModel>(IQueryRequest<TEntity, TModel> request, CancellationToken ct = default)
            where TEntity : class => throw new NotSupportedException();

        public IAsyncEnumerable<TEntity> QueryAsync<TEntity>(IAsyncQueryRequest<TEntity> request, CancellationToken ct = default)
            where TEntity : class => throw new NotSupportedException();

        public IAsyncEnumerable<TModel> QueryAsync<TEntity, TModel>(IAsyncQueryRequest<TEntity, TModel> request, CancellationToken ct = default)
            where TEntity : class => throw new NotSupportedException();

        public Task<Result> SendAsync(ICommandRequest request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<Result<TResponse>> SendAsync<TResponse>(ICommandRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public void AddHint<THint>(THint hint) where THint : Enum => throw new NotSupportedException();

        public object GetService(Type serviceType) => throw new NotSupportedException();

        public T GetService<T>() => throw new NotSupportedException();
    }
}
