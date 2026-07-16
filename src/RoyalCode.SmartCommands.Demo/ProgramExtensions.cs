using Microsoft.AspNetCore.Mvc;
using RoyalCode.SmartCommands.Demo.Commands.Estoques;
using RoyalCode.SmartCommands.Demo.Commands.Pedidos;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartCommands.WorkContext.Extensions;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartProblems.HttpResults;
using RoyalCode.SmartSearch;
using RoyalCode.SmartSearch.AspNetCore.HttpResults;
using RoyalCode.SmartSearch.AspNetCore.Internals;
using RoyalCode.SmartSearch.Defaults;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo;

[MapApiHandlers, AddHandlersServices("")]
public static partial class ProgramExtensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddHandlersServices<IWorkContext>();
        builder.Services.AddConcurrencyRetryProblem<EditarProduto>(
            "demo.produtos.editar",
            static (_, _) => Problems.InvalidState(
                "O produto foi alterado por outro processo.",
                typeId: "demo.concurrency_conflict"));
        builder.Services.AddConcurrencyRetryProblem<AdicionarEntradaEstoque>(
            "demo.estoques.adicionar",
            static (_, _) => Problems.InvalidState(
                "O estoque foi alterado por outro processo.",
                typeId: "demo.estoque.concurrency_conflict"));
        builder.Services.AddConcurrencyRetryProblem<ReservarEstoque>(
            "demo.estoques.reservar",
            static (_, _) => Problems.InvalidState(
                "O estoque foi alterado por outro processo.",
                typeId: "demo.estoque.concurrency_conflict"));
        builder.Services.AddConcurrencyRetryProblem<LiberarReservaEstoque>(
            "demo.estoques.liberar",
            static (_, _) => Problems.InvalidState(
                "O estoque foi alterado por outro processo.",
                typeId: "demo.estoque.concurrency_conflict"));
        builder.Services.AddConcurrencyRetryProblem<CancelarPedido>(
            "demo.pedidos.cancelar",
            static (_, _) => Problems.InvalidState(
                "O pedido ou o estoque foi alterado por outro processo.",
                typeId: "demo.pedido.concurrency_conflict"));
        builder.Services.AddConcurrencyRetryProblem<CriarPedido>(
            "demo.pedidos.criar",
            static (_, _) => Problems.InvalidState(
                "O estoque foi alterado por outro processo durante a criacao do pedido.",
                typeId: "demo.pedido.concurrency_conflict"));
        builder.Services.AddTransient<SomeService>();

        // serviço do comando de demonstração de binding (playground/{movieId}/views)
        builder.Services.AddSingleton<Commands.Movies.IRelogioDemo, Commands.Movies.RelogioDemo>();

        // Catalogo RFC 9457 dos typeIds customizados do dominio; alimenta a conversao para ProblemDetails
        // e a pagina de documentacao publicada em /.problems (ver ConfigurePipeline).
        builder.Services.AddProblemDetailsDescriptions(ProblemDetailsCatalog.Configure);

        builder.Services.AddWorkContext<DemoDbContext>()
            .AddUnitOfWorkAccessor()
            .ConfigureDbContext()
            .ConfigureRepositories(repos =>
            {
                repos.Add<Produto>();
                repos.Add<ProdutoEstoque>();
                repos.Add<Pedido>();
                repos.Add<Loja>();
            })
            .ConfigureSearches(searches =>
            {
                searches.Add<Produto>();
                searches.Add<Pedido>();
                searches.Add<Loja>();
            });
    }

    public static void ConfigurePipeline(this WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapProblemDetailsDescriptionPage(); // GET /.problems
        }

        var produtosGroup = app.MapProdutosGroup().WithTags("Produtos");
        var pedidosGroup = app.MapPedidosGroup().WithTags("Pedidos");
        var lojasGroup = app.MapLojasGroup().WithTags("Lojas");
        // grupo de demonstração do binding de parâmetros externos (Fase 5/DF2/DF3);
        // o grupo Movies segue não mapeado: os endpoints de Review não têm search/repositório registrados
        var playgroundGroup = app.MapPlaygroundGroup().WithTags("Playground");


        // como seria um find
        produtosGroup.MapGet("manual/{id:guid}", FindProdutoAsync);


        // com seria um search
        produtosGroup.MapGet("search/manual", SearchProdutoProdutoFiltroAsync);
        produtosGroup.MapSearch<Produto, ProdutoDetalhes, ProdutoFiltro>("search/auto");

        var x = Exemplo_SearchProdutoAsync;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter, ProblemCategory.InternalServerError)]
    private static Task<MatchSearch<ProdutoDetalhes>> SearchProdutoProdutoFiltroAsync(
        [AsParameters] ProdutoFiltro filter,
        [AsParameters] SearchOptions options,
        [FromQuery] Sorting[]? orderby,
        [FromServices] ICriteria<Produto> criteria,
        [FromServices] ILogger<ICriteria<Produto>> logger,
        CancellationToken ct)
    {
        Action<ICriteria<Produto>>? configure = null;

        return Performer.SearchAsync<Produto, ProdutoDetalhes, ProdutoFiltro>(
            filter, options, orderby, criteria, configure, logger, ct);
    }

    [ProduceProblems(ProblemCategory.InvalidParameter, ProblemCategory.InternalServerError)]
    private static Task<MatchSearch<ProdutoDetalhes>> Exemplo_SearchProdutoAsync(
        [AsParameters] ExemploProdutoFiltro filter,
        [AsParameters] SearchOptions options,
        [FromQuery] Sorting[]? orderby,
        [FromServices] ICriteria<Produto> criteria,
        [FromServices] ILogger<ICriteria<Produto>> logger,
        [FromServices] SomeService some,
        [FromRoute] int id,
        HttpContext context,
        CancellationToken ct)
    {
        Action<ICriteria<Produto>>? configure = (criteria) =>
        {
            filter.ConfigureSearch(criteria, context, some, id);
        };

        Func<ICriteria<Produto>, Task>? conf = async (criteria) =>
        {
            await filter.AnotherMethod();
        };

        return Performer.SearchAsync<Produto, ProdutoDetalhes, ExemploProdutoFiltro>(
            filter, options, orderby, criteria, configure, logger, ct);
    }

    [ProduceProblems(ProblemCategory.NotFound)]
    private static async Task<OkMatch<ProdutoDetalhes>> FindProdutoAsync(
        [FromRoute] Id<Produto, Guid> id, 
        [FromServices] IRepositoryAccessor<Produto> accessor,
        CancellationToken ct)
    {
        var findResult = await accessor.FindEntityAsync<ProdutoDetalhes, Guid>(id, ct);
        if (findResult.NotFound(out var notfoundProblem))
            return notfoundProblem;
        return findResult.Entity;
    }
}
