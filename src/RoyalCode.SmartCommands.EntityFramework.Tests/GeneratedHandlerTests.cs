using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.EntityFramework.Adapters;
using RoyalCode.SmartCommands.EntityFramework.Extensions;
using RoyalCode.SmartCommands.EntityFramework.Options;
using RoyalCode.SmartCommands.EntityFramework.Tests.Commands;
using RoyalCode.SmartCommands.EntityFramework.Tests.Support;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.EntityFramework.Tests;

/// <summary>
/// Uso direto do handler gerado (fora de HTTP): o generator roda como analyzer sobre este projeto,
/// e o handler real consome <c>DbContextAccessor&lt;TestDbContext&gt;</c> registrado por
/// <c>AddUnitOfWorkAccessor</c>. Valida DF14 na perspectiva do chamador do handler.
/// </summary>
public class GeneratedHandlerTests
{
    private static ServiceProvider BuildProvider(SqliteDatabase database, bool beginTransactions)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => database.CreateContext());
        services.AddUnitOfWorkAccessor<TestDbContext>();
        services.Configure<DbContextAdapterOptions>(o => o.BeginTransactions = beginTransactions);
        services.AddGadgetHandlersServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Handler_gerado_persiste_e_retorna_sucesso()
    {
        using var database = new SqliteDatabase();
        await using var provider = BuildProvider(database, beginTransactions: true);
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICriarGadgetHandler>();
        var result = await handler.HandleAsync(new CriarGadget { Nome = "g1" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, database.TransactionInterceptor.CommitCount);
        Assert.Equal(1, database.CountGadgets());
    }

    [Fact]
    public async Task Excecao_inesperada_atravessa_o_handler_gerado_sem_virar_result()
    {
        using var database = new SqliteDatabase();
        await using var provider = BuildProvider(database, beginTransactions: true);
        using var scope = provider.CreateScope();

        var saveFailure = new InvalidOperationException("save boom");
        database.SaveInterceptor.Throw = saveFailure;

        var handler = scope.ServiceProvider.GetRequiredService<ICriarGadgetHandler>();
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new CriarGadget { Nome = "g1" }, CancellationToken.None));

        Assert.Same(saveFailure, thrown);
        Assert.Equal(1, database.TransactionInterceptor.RollbackCount);

        database.SaveInterceptor.Throw = null;
        Assert.Equal(0, database.CountGadgets());
    }

    [Fact]
    public async Task Cancelamento_atravessa_o_handler_gerado_como_cancelamento()
    {
        using var database = new SqliteDatabase();
        var id = database.SeedGadget("original");
        await using var provider = BuildProvider(database, beginTransactions: true);
        using var scope = provider.CreateScope();

        using var cts = new CancellationTokenSource();
        var command = new RenomearGadget
        {
            NovoNome = "novo",
            // cancela entre o find e o save: o CompleteAsync recebe o token já cancelado
            AntesDeSalvar = cts.Cancel,
        };

        var handler = scope.ServiceProvider.GetRequiredService<IRenomearGadgetHandler>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(id, command, cts.Token));

        // o rollback rodou com token próprio, mesmo com o token do handler cancelado
        Assert.Equal(1, database.TransactionInterceptor.RollbackCount);

        using var db = database.CreateContext();
        Assert.Equal("original", db.Gadgets.Single(g => g.Id == id).Nome);
    }

    [Fact]
    public async Task WithTransaction_exige_transacao_mesmo_com_a_opcao_global_desligada()
    {
        using var database = new SqliteDatabase();
        await using var provider = BuildProvider(database, beginTransactions: false);
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICriarGadgetTransacionalHandler>();
        var result = await handler.HandleAsync(new CriarGadgetTransacional { Nome = "g1" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // DF21: a transação foi criada e commitada por exigência do comando, não da opção
        Assert.Equal(1, database.TransactionInterceptor.CommitCount);
        Assert.Equal(1, database.CountGadgets());
    }

    [Fact]
    public async Task WithTransaction_faz_rollback_da_transacao_exigida_em_falha()
    {
        using var database = new SqliteDatabase();
        await using var provider = BuildProvider(database, beginTransactions: false);
        using var scope = provider.CreateScope();

        var saveFailure = new InvalidOperationException("save boom");
        database.SaveInterceptor.Throw = saveFailure;

        var handler = scope.ServiceProvider.GetRequiredService<ICriarGadgetTransacionalHandler>();
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new CriarGadgetTransacional { Nome = "g1" }, CancellationToken.None));

        Assert.Same(saveFailure, thrown);
        Assert.Equal(1, database.TransactionInterceptor.RollbackCount);

        database.SaveInterceptor.Throw = null;
        Assert.Equal(0, database.CountGadgets());
    }

    [Fact]
    public async Task Conflito_otimista_real_chega_como_problema_ao_chamador()
    {
        using var database = new SqliteDatabase();
        var id = database.SeedGadget("original");
        // sem transação explícita: a escrita rival ocorre entre o find e o save do handler,
        // e os contextos compartilham a mesma conexão SQLite
        await using var provider = BuildProvider(database, beginTransactions: false);
        using var scope = provider.CreateScope();

        var command = new RenomearGadget
        {
            NovoNome = "meu nome",
            AntesDeSalvar = () => database.UpdateGadgetByRival(id),
        };

        var handler = scope.ServiceProvider.GetRequiredService<IRenomearGadgetHandler>();
        var result = await handler.HandleAsync(id, command, CancellationToken.None);

        Assert.True(result.HasProblems(out var problems));
        var problem = Assert.Single(problems!);
        Assert.Equal(ProblemCategory.InvalidState, problem.Category);
        Assert.Equal(DbContextAccessor<TestDbContext>.ConcurrencyConflictDetail, problem.Detail);

        // o estado persistido é o do rival; a mensagem não expõe detalhe do provider
        using var db = database.CreateContext();
        Assert.Equal("original (rival)", db.Gadgets.Single(g => g.Id == id).Nome);
    }
}
