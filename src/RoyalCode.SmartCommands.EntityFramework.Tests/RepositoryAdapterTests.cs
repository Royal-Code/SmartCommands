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
}
