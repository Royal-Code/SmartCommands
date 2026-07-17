using RoyalCode.SmartCommands.EntityFramework.Adapters;
using RoyalCode.SmartCommands.EntityFramework.Tests.Support;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.EntityFramework.Tests;

/// <summary>
/// Testes do contrato DF14 de <see cref="DbContextAccessor{TContext}"/> sobre SQLite real:
/// sucesso salva e commita uma vez; falha inesperada tenta rollback e é relançada;
/// cancelamento permanece cancelamento; conflito otimista é o único problema conhecido.
/// </summary>
public class DbContextAccessorTests
{
    [Fact]
    public async Task Sucesso_salva_e_commita_uma_unica_vez()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: true);

        await accessor.BeginAsync(CancellationToken.None);
        await accessor.AddEntityAsync(new Gadget { Id = Guid.NewGuid(), Nome = "g1" }, CancellationToken.None);
        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, database.TransactionInterceptor.CommitCount);
        Assert.Equal(0, database.TransactionInterceptor.RollbackCount);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Equal(1, database.CountGadgets());
    }

    [Fact]
    public async Task Sucesso_sem_transacao_apenas_salva()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: false);

        await accessor.BeginAsync(CancellationToken.None);
        await accessor.AddEntityAsync(new Gadget { Id = Guid.NewGuid(), Nome = "g1" }, CancellationToken.None);
        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, database.TransactionInterceptor.CommitCount);
        Assert.Equal(1, database.CountGadgets());
    }

    [Fact]
    public async Task Falha_inesperada_no_save_relanca_a_mesma_excecao_apos_rollback()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: true);

        await accessor.BeginAsync(CancellationToken.None);
        await accessor.AddEntityAsync(new Gadget { Id = Guid.NewGuid(), Nome = "g1" }, CancellationToken.None);

        var saveFailure = new InvalidOperationException("save boom");
        database.SaveInterceptor.Throw = saveFailure;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => accessor.CompleteAsync(CancellationToken.None));

        // a exceção original atravessa sem wrapper e sem virar Result
        Assert.Same(saveFailure, thrown);
        Assert.Equal(1, database.TransactionInterceptor.RollbackCount);
        Assert.Equal(0, database.TransactionInterceptor.CommitCount);

        database.SaveInterceptor.Throw = null;
        Assert.Equal(0, database.CountGadgets());
    }

    [Fact]
    public async Task Falha_inesperada_sem_transacao_do_adapter_relanca_sem_tentar_rollback()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: false);

        await accessor.AddEntityAsync(new Gadget { Id = Guid.NewGuid(), Nome = "g1" }, CancellationToken.None);

        var saveFailure = new InvalidOperationException("save boom");
        database.SaveInterceptor.Throw = saveFailure;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => accessor.CompleteAsync(CancellationToken.None));

        Assert.Same(saveFailure, thrown);
        Assert.Equal(0, database.TransactionInterceptor.RollbackCount);
    }

    [Fact]
    public async Task Transacao_do_usuario_nao_e_commitada_nem_revertida_pelo_adapter()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: true);

        // o usuário abre a própria transação, sem BeginAsync: o adapter não é o dono dela
        await using var userTransaction = await db.Database.BeginTransactionAsync();
        db.Gadgets.Add(new Gadget { Id = Guid.NewGuid(), Nome = "g1" });

        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, database.TransactionInterceptor.CommitCount);
        Assert.NotNull(db.Database.CurrentTransaction);

        // e em falha, o adapter também não reverte a transação do usuário
        database.SaveInterceptor.Throw = new InvalidOperationException("save boom");
        db.Gadgets.Add(new Gadget { Id = Guid.NewGuid(), Nome = "g2" });
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => accessor.CompleteAsync(CancellationToken.None));

        Assert.Equal(0, database.TransactionInterceptor.RollbackCount);
        Assert.NotNull(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Falha_no_commit_relanca_a_mesma_excecao_apos_rollback()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: true);

        await accessor.BeginAsync(CancellationToken.None);
        await accessor.AddEntityAsync(new Gadget { Id = Guid.NewGuid(), Nome = "g1" }, CancellationToken.None);

        var commitFailure = new IOException("commit boom");
        database.TransactionInterceptor.ThrowOnCommit = commitFailure;

        var thrown = await Assert.ThrowsAsync<IOException>(
            () => accessor.CompleteAsync(CancellationToken.None));

        Assert.Same(commitFailure, thrown);
        Assert.Equal(1, database.TransactionInterceptor.RollbackCount);

        database.TransactionInterceptor.ThrowOnCommit = null;
        Assert.Equal(0, database.CountGadgets());
    }

    [Fact]
    public async Task Falha_no_rollback_preserva_as_duas_excecoes()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: true);

        await accessor.BeginAsync(CancellationToken.None);
        await accessor.AddEntityAsync(new Gadget { Id = Guid.NewGuid(), Nome = "g1" }, CancellationToken.None);

        var saveFailure = new InvalidOperationException("save boom");
        var rollbackFailure = new IOException("rollback boom");
        database.SaveInterceptor.Throw = saveFailure;
        database.TransactionInterceptor.ThrowOnRollback = rollbackFailure;

        var aggregate = await Assert.ThrowsAsync<AggregateException>(
            () => accessor.CompleteAsync(CancellationToken.None));

        // primeira inner é a falha primária (save/commit); a segunda é a falha do rollback
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Same(saveFailure, aggregate.InnerExceptions[0]);
        Assert.Same(rollbackFailure, aggregate.InnerExceptions[1]);
    }

    [Fact]
    public async Task Cancelamento_permanece_cancelamento_e_o_rollback_usa_token_proprio()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: true);

        await accessor.BeginAsync(CancellationToken.None);
        await accessor.AddEntityAsync(new Gadget { Id = Guid.NewGuid(), Nome = "g1" }, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // não vira Result nem AggregateException: o cancelamento atravessa como OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => accessor.CompleteAsync(cts.Token));

        // o cleanup rodou mesmo com o token do handler já cancelado (token próprio do rollback)
        Assert.Equal(1, database.TransactionInterceptor.RollbackCount);
        Assert.Equal(0, database.CountGadgets());
    }

    [Fact]
    public async Task Conflito_de_concorrencia_retorna_problema_conhecido_sem_lancar()
    {
        using var database = new SqliteDatabase();
        var id = database.SeedGadget("original");

        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: false);

        var gadget = db.Gadgets.Single(g => g.Id == id);
        database.UpdateGadgetByRival(id);

        gadget.Nome = "alterado";
        gadget.Versao++;
        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.HasProblems(out var problems));
        var problem = Assert.Single(problems!);
        Assert.Equal(ProblemCategory.InvalidState, problem.Category);
        Assert.Equal(DbContextAccessor<TestDbContext>.ConcurrencyConflictDetail, problem.Detail);
    }

    [Fact]
    public async Task Conflito_de_concorrencia_com_transacao_faz_rollback_e_retorna_problema()
    {
        using var database = new SqliteDatabase();
        var id = database.SeedGadget("original");

        using var db = database.CreateContext();
        var accessor = database.CreateAccessor(db, beginTransactions: true);

        // o rival atualiza antes do begin: os contextos compartilham a mesma conexão SQLite,
        // então a escrita rival não pode ocorrer enquanto a transação do adapter está aberta
        var gadget = db.Gadgets.Single(g => g.Id == id);
        database.UpdateGadgetByRival(id);
        await accessor.BeginAsync(CancellationToken.None);

        gadget.Nome = "alterado";
        gadget.Versao++;
        var result = await accessor.CompleteAsync(CancellationToken.None);

        Assert.True(result.HasProblems(out var problems));
        var problem = Assert.Single(problems!);
        Assert.Equal(ProblemCategory.InvalidState, problem.Category);
        Assert.Equal(1, database.TransactionInterceptor.RollbackCount);
        Assert.Null(db.Database.CurrentTransaction);
    }
}
