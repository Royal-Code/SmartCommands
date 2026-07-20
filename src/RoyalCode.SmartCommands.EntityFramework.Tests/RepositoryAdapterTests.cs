using RoyalCode.SmartCommands.EntityFramework.Adapters;
using RoyalCode.SmartCommands.EntityFramework.Tests.Support;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;

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
            System.Linq.Expressions.Expression<Func<Gadget, bool>> filter,
            IReadOnlyList<FindCriterion> criteria,
            CancellationToken ct)
        {
            // o teste projeta somente para GadgetNome; o helper protegido executa a
            // consulta única no provider e gera o NotFound nomeando a entidade
            System.Linq.Expressions.Expression<Func<Gadget, GadgetNome>> selector =
                g => new GadgetNome { Nome = g.Nome };

            return FindEntityAsync(
                filter,
                criteria,
                (System.Linq.Expressions.Expression<Func<Gadget, TDto>>)(object)selector,
                ct);
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
}
