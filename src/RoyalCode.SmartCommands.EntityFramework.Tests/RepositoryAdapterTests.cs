using Microsoft.EntityFrameworkCore.Diagnostics;
using RoyalCode.SmartCommands.EntityFramework.Adapters;
using RoyalCode.SmartCommands.EntityFramework.Tests.Support;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using System.Data.Common;
using System.Linq.Expressions;

namespace RoyalCode.SmartCommands.EntityFramework.Tests;

/// <summary>
/// Testes de <see cref="RepositoryAdapter{TEntity, TContext}"/>: o contexto tipado é exposto
/// às subclasses (<c>protected TContext Context</c>) e as projeções o utilizam sem guardar
/// uma segunda referência ao mesmo contexto.
/// </summary>
public class RepositoryAdapterTests
{
    private sealed class GadgetRepository : RepositoryAdapter<Gadget, TestDbContext>
    {
        public GadgetRepository(TestDbContext db) : base(db) { }

        public override async Task<FindResult<TDto, TId>> FindEntityAsync<TDto, TId>(Id<Gadget, TId> id, CancellationToken ct)
        {
            // projeção implementada com o contexto tipado herdado, sem campo próprio de contexto
            var gadget = await Context.Gadgets.FindAsync([id.Value], ct);

            if (gadget is null)
                return new FindResult<TDto, TId>(id.Value);

            return new FindResult<TDto, TId>((TDto)(object)new GadgetNome { Nome = gadget.Nome }, id.Value);
        }

        public override Task<FindResult<TDto>> FindEntityAsync<TDto>(
            Expression<Func<Gadget, bool>> filter,
            IReadOnlyList<FindCriterion> criteria,
            CancellationToken ct)
        {
            // O helper protegido executa a consulta única no provider e gera o NotFound nomeando
            // a entidade. GadgetEnvelope exercita explicitamente uma projeção que contém entidade.
            Expression<Func<Gadget, TDto>> selector;
            if (typeof(TDto) == typeof(GadgetEnvelope))
            {
                Expression<Func<Gadget, GadgetEnvelope>> envelope =
                    g => new GadgetEnvelope { Entity = g };
                selector = (Expression<Func<Gadget, TDto>>)(object)envelope;
            }
            else
            {
                Expression<Func<Gadget, GadgetNome>> name =
                    g => new GadgetNome { Nome = g.Nome };
                selector = (Expression<Func<Gadget, TDto>>)(object)name;
            }

            return FindEntityAsync(
                filter,
                criteria,
                selector,
                ct);
        }
    }

    private sealed class QueryCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task Find_por_id_retorna_a_entidade()
    {
        using var database = new SqliteDatabase();
        var id = database.SeedGadget("g1");

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<Guid>(id, CancellationToken.None);

        Assert.False(found.NotFound(out _));
        Assert.Equal("g1", found.Entity!.Nome);
    }

    [Fact]
    public async Task Find_por_id_inexistente_retorna_notfound()
    {
        using var database = new SqliteDatabase();

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<Guid>(Guid.NewGuid(), CancellationToken.None);

        Assert.True(found.NotFound(out var problem));
        Assert.Equal(ProblemCategory.NotFound, problem!.Category);
    }

    [Fact]
    public async Task Projecao_para_dto_usa_o_contexto_tipado()
    {
        using var database = new SqliteDatabase();
        var id = database.SeedGadget("g1");

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<GadgetNome, Guid>(id, CancellationToken.None);

        Assert.False(found.NotFound(out _));
        Assert.Equal("g1", found.Entity!.Nome);
    }

    [Fact]
    public async Task Projecao_de_id_inexistente_retorna_notfound()
    {
        using var database = new SqliteDatabase();

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<GadgetNome, Guid>(Guid.NewGuid(), CancellationToken.None);

        Assert.True(found.NotFound(out var problem));
        Assert.Equal(ProblemCategory.NotFound, problem!.Category);
    }

    [Fact]
    public void Contexto_nulo_lanca_argumentnull()
    {
        Assert.Throws<ArgumentNullException>(() => new GadgetRepository(null!));
    }

    [Fact]
    public async Task Projecao_por_predicado_encontra_e_projeta()
    {
        using var database = new SqliteDatabase();
        database.SeedGadget("g1");

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<GadgetNome>(
            g => g.Nome == "g1",
            [new FindCriterion(nameof(Gadget.Nome), "g1")],
            CancellationToken.None);

        Assert.False(found.NotFound(out _));
        Assert.Equal("g1", found.Entity!.Nome);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Projecao_por_predicado_notfound_nomeia_a_entidade_com_criterios_na_ordem()
    {
        using var database = new SqliteDatabase();

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<GadgetNome>(
            g => g.Nome == "nope" && g.Versao == 7,
            [new FindCriterion(nameof(Gadget.Nome), "nope"), new FindCriterion(nameof(Gadget.Versao), 7)],
            CancellationToken.None);

        Assert.True(found.NotFound(out var problem));
        Assert.Equal(ProblemCategory.NotFound, problem!.Category);
        Assert.Equal("The record of 'Gadget' with Nome 'nope', Versao '7' was not found", problem.Detail);
        Assert.Equal(nameof(Gadget), problem.Extensions!["entity"]);
    }

    [Fact]
    public async Task Projecao_por_predicado_propaga_cancelamento()
    {
        using var database = new SqliteDatabase();
        database.SeedGadget("g1");

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.FindEntityAsync<GadgetNome>(
                g => g.Nome == "g1",
                [new FindCriterion(nameof(Gadget.Nome), "g1")],
                cts.Token));
    }

    [Fact]
    public async Task Projecao_por_predicado_preserva_query_filter()
    {
        using var database = new SqliteDatabase();
        database.SeedGadget("Hidden");

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<GadgetNome>(
            g => g.Nome == "Hidden",
            [new FindCriterion(nameof(Gadget.Nome), "Hidden")],
            CancellationToken.None);

        Assert.True(found.NotFound(out _));
    }

    [Fact]
    public async Task Projecao_por_predicado_com_duplicatas_retorna_a_primeira()
    {
        using var database = new SqliteDatabase();
        database.SeedGadget("duplicate");
        database.SeedGadget("duplicate");

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<GadgetNome>(
            g => g.Nome == "duplicate",
            [new FindCriterion(nameof(Gadget.Nome), "duplicate")],
            CancellationToken.None);

        Assert.True(found.Found);
        Assert.Equal("duplicate", found.Entity!.Nome);
    }

    [Fact]
    public async Task Projecao_por_predicado_preserva_criterio_nulo_no_notfound()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);
        string? name = null;

        var found = await repository.FindEntityAsync<GadgetNome>(
            g => g.Nome == name,
            [new FindCriterion(nameof(Gadget.Nome), name)],
            CancellationToken.None);

        Assert.True(found.NotFound(out var problem));
        Assert.True(problem!.Extensions!.ContainsKey(nameof(Gadget.Nome)));
        Assert.Null(problem.Extensions[nameof(Gadget.Nome)]);
    }

    [Fact]
    public async Task Projecao_por_predicado_executa_uma_query_com_apenas_as_colunas_do_dto()
    {
        using var database = new SqliteDatabase();
        database.SeedGadget("g1");
        var interceptor = new QueryCaptureInterceptor();

        using var db = database.CreateContext(interceptor);
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<GadgetNome>(
            g => g.Nome == "g1",
            [new FindCriterion(nameof(Gadget.Nome), "g1")],
            CancellationToken.None);

        Assert.True(found.Found);
        var command = Assert.Single(interceptor.Commands);
        var selectClause = command[..command.IndexOf("FROM", StringComparison.OrdinalIgnoreCase)];
        Assert.Contains("Nome", selectClause);
        Assert.DoesNotContain("Versao", selectClause);
        Assert.DoesNotContain("Id", selectClause);
    }

    [Fact]
    public async Task Projecao_que_contem_entidade_continua_sem_tracking()
    {
        using var database = new SqliteDatabase();
        database.SeedGadget("g1");

        using var db = database.CreateContext();
        var repository = new GadgetRepository(db);

        var found = await repository.FindEntityAsync<GadgetEnvelope>(
            g => g.Nome == "g1",
            [new FindCriterion(nameof(Gadget.Nome), "g1")],
            CancellationToken.None);

        Assert.True(found.Found);
        Assert.Equal("g1", found.Entity!.Entity.Nome);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
